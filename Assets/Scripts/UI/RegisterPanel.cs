using System.Collections;
using SuperviseSoft.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace SuperviseSoft.UI
{
  public sealed class RegisterPanel : MonoBehaviour
  {
    private const int SmsCooldownSeconds = 60;

    public GameObject root;
    public InputField phoneInput;
    public InputField smsCodeInput;
    public InputField passwordInput;
    public InputField confirmPasswordInput;
    public Button sendSmsCodeButton;
    public Button registerButton;
    public Button backToLoginButton;
    public Text messageText;
    public LoginPanel loginPanel;
    public MainEntryPanel mainEntryPanel;

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
      EnsurePanelReferences();
      if (_bound)
      {
        return;
      }

      if (sendSmsCodeButton == null && registerButton == null && backToLoginButton == null)
      {
        return;
      }

      _bound = true;
      _sendButtonText = sendSmsCodeButton == null ? null : sendSmsCodeButton.GetComponentInChildren<Text>();

      sendSmsCodeButton?.onClick.AddListener(OnSendSmsCodeClicked);
      registerButton?.onClick.AddListener(OnRegisterClicked);
      backToLoginButton?.onClick.AddListener(() =>
      {
        EnsurePanelReferences();
        Hide();
        mainEntryPanel?.Hide();
        loginPanel?.Show();
      });
    }

    public void Show()
    {
      PanelVisibility.Show(root, gameObject);
      SetMessage(string.Empty);
    }

    public void Hide()
    {
      PanelVisibility.Hide(root, gameObject);
    }

    private void OnSendSmsCodeClicked()
    {
      var phone = GetPhone();
      if (!LoginPanel.IsValidPhone(phone))
      {
        SetMessage("手机号格式不正确。");
        return;
      }

      SetButtonsInteractable(false);
      StartCoroutine(AuthService.Instance.SendSmsCode(phone, "register", response =>
      {
        SetButtonsInteractable(true);
        if (response.success)
        {
          SetMessage("验证码已发送。");
          StartCooldown();
          return;
        }

        SetMessage(response.message);
      }));
    }

    private void OnRegisterClicked()
    {
      var phone = GetPhone();
      var code = smsCodeInput == null ? string.Empty : smsCodeInput.text.Trim();
      var password = passwordInput == null ? string.Empty : passwordInput.text;
      var confirmPassword = confirmPasswordInput == null ? string.Empty : confirmPasswordInput.text;

      if (!LoginPanel.IsValidPhone(phone))
      {
        SetMessage("手机号格式不正确。");
        return;
      }

      if (string.IsNullOrWhiteSpace(code))
      {
        SetMessage("请输入短信验证码。");
        return;
      }

      if (!LoginPanel.IsValidPassword(password))
      {
        SetMessage("密码至少需要 6 位。");
        return;
      }

      if (password != confirmPassword)
      {
        SetMessage("两次输入的密码不一致。");
        return;
      }

      SetButtonsInteractable(false);
      StartCoroutine(AuthService.Instance.RegisterWithPhoneCodeAndPassword(phone, code, password, response =>
      {
        SetButtonsInteractable(true);
        if (response.success)
        {
          EnsurePanelReferences();
          SetMessage("注册成功。");
          Hide();
          loginPanel?.Hide();
          mainEntryPanel?.ShowAndRefresh();
          return;
        }

        SetMessage(response.message);
      }));
    }

    private void StartCooldown()
    {
      if (_countdownCoroutine != null)
      {
        StopCoroutine(_countdownCoroutine);
      }

      _countdownCoroutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
      if (sendSmsCodeButton != null)
      {
        sendSmsCodeButton.interactable = false;
      }

      for (var remaining = SmsCooldownSeconds; remaining > 0; remaining--)
      {
        if (_sendButtonText != null)
        {
          _sendButtonText.text = $"{remaining}s";
        }

        yield return new WaitForSeconds(1f);
      }

      if (_sendButtonText != null)
      {
        _sendButtonText.text = "发送验证码";
      }

      if (sendSmsCodeButton != null)
      {
        sendSmsCodeButton.interactable = true;
      }

      _countdownCoroutine = null;
    }

    private void EnsurePanelReferences()
    {
      if (loginPanel == null)
      {
        loginPanel = Object.FindObjectOfType<LoginPanel>(true);
      }

      if (mainEntryPanel == null)
      {
        mainEntryPanel = Object.FindObjectOfType<MainEntryPanel>(true);
      }
    }

    private void SetButtonsInteractable(bool value)
    {
      if (registerButton != null)
      {
        registerButton.interactable = value;
      }

      if (sendSmsCodeButton != null && _countdownCoroutine == null)
      {
        sendSmsCodeButton.interactable = value;
      }
    }

    private string GetPhone()
    {
      return phoneInput == null ? string.Empty : phoneInput.text.Trim();
    }

    private void SetMessage(string message)
    {
      if (messageText != null)
      {
        messageText.text = message ?? string.Empty;
      }
    }
  }
}
