using System.Collections;
using System.Text.RegularExpressions;
using SuperviseSoft.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace SuperviseSoft.UI
{
    public sealed class LoginPanel:MonoBehaviour
    {
        private const int SmsCooldownSeconds = 60;
        private static readonly Regex PhoneRegex = new Regex(@"^(?:\+?86)?1[3-9]\d{9}$");

        public GameObject root;
        public InputField phoneInput;
        public InputField smsCodeInput;
        public InputField passwordInput;
        /// <summary>
        /// 发送验证码
        /// </summary>
        public Button sendSmsCodeButton;
        /// <summary>
        /// 验证码登录
        /// </summary>
        public Button smsLoginButton;
        /// <summary>
        /// 密码登录
        /// </summary>
        public Button passwordLoginButton;
        /// <summary>
        /// 注册
        /// </summary>
        public Button openRegisterButton;
        /// <summary>
        /// 忘记密码
        /// </summary>
        public Button openResetPasswordButton;
        public Text messageText;
        public MainEntryPanel mainEntryPanel;
        public RegisterPanel registerPanel;
        public ResetPasswordPanel resetPasswordPanel;

        private Coroutine _countdownCoroutine;
        private Text _sendButtonText;
        private bool _bound;

        private void Awake()
        {
            Bind();
        }

        private void Start()
        {
            Bind();
        }

        public void Bind()
        {
            //EnsurePanelReferences();
            if(_bound)
            {
                return;
            }

            //if (sendSmsCodeButton == null &&
            //    smsLoginButton == null &&
            //    passwordLoginButton == null &&
            //    openRegisterButton == null &&
            //    openResetPasswordButton == null)
            //{
            //  return;
            //}

            _bound = true;
            _sendButtonText = sendSmsCodeButton == null ? null : sendSmsCodeButton.GetComponentInChildren<Text>();

            sendSmsCodeButton?.onClick.AddListener(OnSendSmsCodeClicked);
            smsLoginButton?.onClick.AddListener(OnSmsLoginClicked);
            passwordLoginButton?.onClick.AddListener(OnPasswordLoginClicked);

            openRegisterButton?.onClick.AddListener(() =>
            {
                //EnsurePanelReferences();
                Hide();
                resetPasswordPanel?.Hide();
                mainEntryPanel?.Hide();
                registerPanel?.Show();
            });

            openResetPasswordButton?.onClick.AddListener(() =>
            {
                //EnsurePanelReferences();
                Hide();
                registerPanel?.Hide();
                mainEntryPanel?.Hide();
                resetPasswordPanel?.Show();
            });
        }

        public void Show()
        {
            PanelVisibility.Show(root,gameObject);
            SetMessage(string.Empty);
        }

        public void Hide()
        {
            PanelVisibility.Hide(root,gameObject);
        }

        private void OnSendSmsCodeClicked()
        {
            var phone = GetPhone();
            if(!IsValidPhone(phone))
            {
                SetMessage("手机号格式不正确。");
                return;
            }

            SetButtonsInteractable(false);
            StartCoroutine(AuthService.Instance.SendSmsCode(phone,"login",response =>
            {
                SetButtonsInteractable(true);
                if(response.success)
                {
                    SetMessage("验证码已发送。");
                    StartCooldown();
                    return;
                }

                SetMessage(response.message);
            }));
        }

        private void OnSmsLoginClicked()
        {
            var phone = GetPhone();
            var code = smsCodeInput == null ? string.Empty : smsCodeInput.text.Trim();

            if(!IsValidPhone(phone))
            {
                SetMessage("手机号格式不正确。");
                return;
            }

            if(string.IsNullOrWhiteSpace(code))
            {
                SetMessage("请输入短信验证码。");
                return;
            }

            SetButtonsInteractable(false);
            StartCoroutine(AuthService.Instance.LoginWithSmsCode(phone,code,response =>
            {
                SetButtonsInteractable(true);
                if(response.success)
                {
                    //EnsurePanelReferences();
                    SetMessage("登录成功。");
                    Hide();
                    registerPanel?.Hide();
                    resetPasswordPanel?.Hide();
                    mainEntryPanel?.ShowAndRefresh();
                    return;
                }

                SetMessage(response.message);
            }));
        }

        private void OnPasswordLoginClicked()
        {
            var phone = GetPhone();
            var password = passwordInput == null ? string.Empty : passwordInput.text;

            if(!IsValidPhone(phone))
            {
                SetMessage("手机号格式不正确。");
                return;
            }

            if(!IsValidPassword(password))
            {
                SetMessage("密码至少需要 6 位。");
                return;
            }

            SetButtonsInteractable(false);
            StartCoroutine(AuthService.Instance.LoginWithPassword(phone,password,response =>
            {
                SetButtonsInteractable(true);
                if(response.success)
                {
                    //EnsurePanelReferences();
                    SetMessage("登录成功。");
                    Hide();
                    registerPanel?.Hide();
                    resetPasswordPanel?.Hide();
                    mainEntryPanel?.ShowAndRefresh();
                    return;
                }

                SetMessage(response.message);
            }));
        }

        private void StartCooldown()
        {
            if(_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
            }

            _countdownCoroutine = StartCoroutine(CooldownRoutine());
        }

        private IEnumerator CooldownRoutine()
        {
            if(sendSmsCodeButton != null)
            {
                sendSmsCodeButton.interactable = false;
            }

            for(var remaining = SmsCooldownSeconds;remaining > 0;remaining--)
            {
                if(_sendButtonText != null)
                {
                    _sendButtonText.text = $"{remaining}s";
                }

                yield return new WaitForSeconds(1f);
            }

            if(_sendButtonText != null)
            {
                _sendButtonText.text = "发送验证码";
            }

            if(sendSmsCodeButton != null)
            {
                sendSmsCodeButton.interactable = true;
            }

            _countdownCoroutine = null;
        }

        //private void EnsurePanelReferences()
        //{
        //  if (mainEntryPanel == null)
        //  {
        //    mainEntryPanel = Object.FindObjectOfType<MainEntryPanel>(true);
        //  }

        //  if (registerPanel == null)
        //  {
        //    registerPanel = Object.FindObjectOfType<RegisterPanel>(true);
        //  }

        //  if (resetPasswordPanel == null)
        //  {
        //    resetPasswordPanel = Object.FindObjectOfType<ResetPasswordPanel>(true);
        //  }
        //}

        private void SetButtonsInteractable( bool value )
        {
            if(smsLoginButton != null)
            {
                smsLoginButton.interactable = value;
            }

            if(passwordLoginButton != null)
            {
                passwordLoginButton.interactable = value;
            }

            if(sendSmsCodeButton != null && _countdownCoroutine == null)
            {
                sendSmsCodeButton.interactable = value;
            }
        }

        private string GetPhone()
        {
            return phoneInput == null ? string.Empty : phoneInput.text.Trim();
        }

        private void SetMessage( string message )
        {
            if(messageText != null)
            {
                messageText.text = message ?? string.Empty;
            }
        }

        public static bool IsValidPhone( string phone )
        {
            return !string.IsNullOrWhiteSpace(phone) && PhoneRegex.IsMatch(phone.Trim().Replace(" ",string.Empty));
        }

        public static bool IsValidPassword( string password )
        {
            return !string.IsNullOrEmpty(password) && password.Length >= 6;
        }
    }
}
