namespace ZauberCMS.Core;

public static class Urls
{
    public const string AdminBaseUrl = "/admin";
    public const string AdminUsersBaseUrl = AdminBaseUrl + "/users";
    public const string AdminMediaBaseUrl = AdminBaseUrl + "/media";
    public const string AdminContentBaseUrl = "/content";
    public const string AdminSettingsBaseUrl = AdminBaseUrl + "/settings";
    public const string AdminFormsBaseUrl = AdminBaseUrl + "/forms";
    public const string RefreshSignIn = "/api/auth/refreshsignin";
        
    public const string AdminCreateContent = AdminBaseUrl + "/createcontent";
    public const string AdminUpdateContent = AdminBaseUrl + "/updatecontent";
        
    public static class Account
    {
            
        public const string ConfirmEmail = "/account/confirmemail";
        public const string ConfirmEmailChange = "/account/confirmemailchange";
        public const string ExternalLogin = "/account/externallogin";
        public const string ForgotPassword = "/account/forgotpassword";
        public const string ForgotPasswordConfirmation = "/account/forgotpasswordconfirmation";
        public const string Login = "/account/login";
        public const string LoginWith2Fa = "/account/loginwith2fa";
        public const string LoginWithRecoveryCode = "/account/loginwithrecoverycode";
        public const string Logout = "/account/logout";
        public const string Register = "/account/register";
        public const string RegisterConfirmation = "/account/registerconfirmation";
        public const string ResendEmailConfirmation = "/account/resendemailconfirmation";
        public const string ResetPassword = "/account/resetpassword";
        public const string ResetPasswordConfirmation = "/account/resetpasswordconfirmation";
        public const string UserProfile = "/account/manage/profile";
    }
}