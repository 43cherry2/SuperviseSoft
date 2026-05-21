# SuperviseSoft First Round Auth

This round only contains phone authentication. It does not include tasks, image upload, COS, or AI analysis.

## CloudBase Console Checklist

1. Enable CloudBase environment.
2. Enable Identity Authentication.
3. Enable phone/SMS verification login in Identity Authentication.
4. Enable username/password login if CloudBase requires a switch for password sign-in.
5. Create database collections: `users`, `auth_login_logs`.
6. Enable HTTP access for these functions and map paths:
   - `POST /sendSmsCode`
   - `POST /registerWithPhone`
   - `POST /loginWithSmsCode`
   - `POST /loginWithPassword`
   - `POST /resetPassword`
   - `GET /getCurrentUser`
   - `POST /logout`

The auth adapter calls CloudBase Auth HTTP API:

- `POST /auth/v1/verification`
- `POST /auth/v1/verification/verify`
- `POST /auth/v1/signup`
- `POST /auth/v1/signin`
- `POST /auth/v1/reset`
- `GET /auth/v1/user/me`
- `POST /auth/v1/user/signout`

Official docs:

- https://docs.cloudbase.net/http-api/basic/overview
- https://docs.cloudbase.net/http-api/auth/auth-send-verification
- https://docs.cloudbase.net/http-api/auth/auth-verify-verification
- https://docs.cloudbase.net/http-api/auth/auth-sign-up
- https://docs.cloudbase.net/http-api/auth/auth-sign-in
- https://docs.cloudbase.net/http-api/auth/auth-reset-password
- https://docs.cloudbase.net/http-api/auth/user-me
- https://docs.cloudbase.net/http-api/auth/auth-sign-out

## Environment Variables

Set these on each cloud function if the runtime cannot infer the environment id:

- `CLOUDBASE_ENV_ID`: your CloudBase envId.
- `CLOUDBASE_AUTH_BASE_URL`: optional override, for example `https://YOUR_ENV.api.tcloudbasegateway.com`.
- `CLOUDBASE_AUTH_REGION`: optional. Use `intl` for international gateway domain.

No Tencent Cloud `SecretId` or `SecretKey` is used in Unity. The Node functions use the CloudBase runtime identity and CloudBase Auth HTTP API.

## Database Collections

`users`

| Field | Description |
| --- | --- |
| `_id` | CloudBase database document id |
| `authUid` | CloudBase Auth user id, from `sub` or `user_id` |
| `phone` | Server-side phone lookup value |
| `phoneMasked` | Masked phone shown to Unity, such as `138****1234` |
| `nickname` | First round default: `student` |
| `role` | First round default: `student` |
| `createdAt` | ISO time string |
| `updatedAt` | ISO time string |
| `lastLoginAt` | ISO time string |

`auth_login_logs`

| Field | Description |
| --- | --- |
| `_id` | CloudBase database document id |
| `authUid` | CloudBase Auth user id if available |
| `phoneMasked` | Masked phone |
| `loginType` | `sms_code`, `password`, `register`, `reset_password`, `logout` |
| `success` | boolean |
| `errorMessage` | failure reason if any |
| `createdAt` | ISO time string |

Passwords are never stored in `users`. Password hashing and verification are delegated to CloudBase Identity Authentication.

## API Examples

All responses use:

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {}
}
```

Error example:

```json
{
  "success": false,
  "code": 40001,
  "message": "error message",
  "data": null
}
```

### POST /sendSmsCode

Request:

```json
{
  "phone": "13800138000",
  "scene": "register"
}
```

`scene` values: `register`, `login`, `reset_password`.

Response:

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {
    "verificationId": "verification_id_from_cloudbase",
    "expiresIn": 600,
    "isUser": false,
    "scene": "register"
  }
}
```

`verificationId` is not an SMS code. Unity caches it in memory and sends it back with the user-entered code.

### POST /registerWithPhone

Request:

```json
{
  "phone": "13800138000",
  "code": "123456",
  "password": "123456",
  "verificationId": "verification_id_from_sendSmsCode"
}
```

Response:

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {
    "session": {
      "authUid": "9876543210123456789",
      "token": "access_token",
      "refreshToken": "refresh_token",
      "tokenType": "Bearer",
      "expiresIn": 7200,
      "expiresAtUnix": 1770000000,
      "phoneMasked": "138****8000"
    },
    "user": {
      "authUid": "9876543210123456789",
      "phoneMasked": "138****8000",
      "nickname": "student",
      "role": "student"
    }
  }
}
```

### POST /loginWithSmsCode

Request:

```json
{
  "phone": "13800138000",
  "code": "123456",
  "verificationId": "verification_id_from_sendSmsCode"
}
```

Response data shape is the same as registration.

### POST /loginWithPassword

Request:

```json
{
  "phone": "13800138000",
  "password": "123456"
}
```

Response data shape is the same as registration.

### POST /resetPassword

Request:

```json
{
  "phone": "13800138000",
  "code": "123456",
  "password": "654321",
  "verificationId": "verification_id_from_sendSmsCode"
}
```

The function resets the password through CloudBase Auth, then signs in with the new password and returns a fresh session.

### GET /getCurrentUser

Request header:

```http
Authorization: Bearer access_token
x-device-id: client_random_device_id
```

Response:

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {
    "authUid": "9876543210123456789",
    "phoneMasked": "138****8000",
    "nickname": "student",
    "role": "student"
  }
}
```

### POST /logout

Request header:

```http
Authorization: Bearer access_token
x-device-id: client_random_device_id
```

Response:

```json
{
  "success": true,
  "code": 0,
  "message": "ok",
  "data": {}
}
```

## Unity Setup

Files generated:

- `Assets/Scripts/Core/AppConfig.cs`
- `Assets/Scripts/Core/ApiResponse.cs`
- `Assets/Scripts/Core/CloudApiClient.cs`
- `Assets/Scripts/Auth/AuthModels.cs`
- `Assets/Scripts/Auth/AuthService.cs`
- `Assets/Scripts/Auth/AuthManager.cs`
- `Assets/Scripts/UI/LoginPanel.cs`
- `Assets/Scripts/UI/RegisterPanel.cs`
- `Assets/Scripts/UI/ResetPasswordPanel.cs`
- `Assets/Scripts/UI/MainEntryPanel.cs`
- `Assets/Scripts/UI/TestSessageSceneBootstrap.cs`
- `Assets/Scripts/Editor/CreateTestSessageScene.cs`
- `Assets/Scenes/TestSessage.unity`

Set `AppConfig.ApiBaseUrl` to your HTTP access base URL. Example:

```csharp
public const string ApiBaseUrl = "https://your-domain-or-env-route";
```

Unity stores only:

- access token
- refresh token
- token type
- authUid
- masked phone
- token expiry
- random `x-device-id`

Unity does not store plaintext passwords, Tencent Cloud secrets, or AI keys.

## Scene UI Binding

`TestSessage.unity` is included in build settings. Because the project was already open in Unity during generation, the scene uses `TestSessageSceneBootstrap` to build and bind the UI at runtime.

If you want to bake the UI into the scene later, run Unity menu:

`SuperviseSoft/Create TestSessage Auth Scene`

Panel responsibilities:

- `LoginPanel`: phone input, SMS code input, password input, send code countdown, SMS login, password login, navigation.
- `RegisterPanel`: phone input, SMS code input, password and confirm password, send code countdown, register.
- `ResetPasswordPanel`: phone input, SMS code input, new password and confirm password, send code countdown, reset.
- `MainEntryPanel`: session check, authUid display, masked phone display, logout.

## Deploy

1. Replace `YOUR_CLOUDBASE_ENV_ID` in `cloudbase/cloudbaserc.json`.
2. In each function directory, install dependencies:

```bash
npm install
```

3. Deploy functions with CloudBase CLI from `cloudbase/`:

```bash
cloudbase functions:deploy sendSmsCode
cloudbase functions:deploy registerWithPhone
cloudbase functions:deploy loginWithSmsCode
cloudbase functions:deploy loginWithPassword
cloudbase functions:deploy resetPassword
cloudbase functions:deploy getCurrentUser
cloudbase functions:deploy logout
```

4. Configure HTTP access path mapping in CloudBase console.
5. Set `Assets/Scripts/Core/AppConfig.cs` `ApiBaseUrl`.

## Test

1. Open `Assets/Scenes/TestSessage.unity`.
2. Play.
3. Enter a valid phone number and click `Send Code`.
4. Register with SMS code and password.
5. Confirm `users` has one record with `authUid`.
6. Confirm `auth_login_logs` has a `register` record.
7. Logout.
8. Log in with SMS code.
9. Logout.
10. Log in with phone/password.
11. Stop Play, Play again, and confirm startup checks the saved login state through `getCurrentUser`.

For later business APIs, do not accept `userId` from Unity. Read `Authorization: Bearer ...`, call CloudBase Auth or the verified runtime context, and use the returned `authUid`.
