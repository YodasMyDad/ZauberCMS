namespace ZauberCMS.Core;

public static class Urls
{
    public const string AdminBaseUrl = "/admin";
    public const string AdminUsersBaseUrl = AdminBaseUrl + "/users";
    public const string AdminMediaBaseUrl = AdminBaseUrl + "/media";
    public const string AdminContentBaseUrl = AdminBaseUrl + "/content";
    public const string AdminSettingsBaseUrl = AdminBaseUrl + "/settings";
    public const string AdminFormsBaseUrl = AdminBaseUrl + "/forms";
    
    public const string ApiRefreshSignIn = "/api/auth/refreshsignin";
    public const string ApiLogout = "/api/auth/logout";
        
    public const string AdminCreateContent = AdminContentBaseUrl + "/createcontent";
    public const string AdminUpdateContent = AdminContentBaseUrl + "/updatecontent";
    
    public const string AdminCreateMedia = AdminMediaBaseUrl + "/createmedia";
    public const string AdminUpdateMedia = AdminMediaBaseUrl + "/updatemedia";
    public const string AdminCreateMediaFolder = AdminMediaBaseUrl + "/createfolder";
    public const string AdminMediaFolder = AdminMediaBaseUrl + "/folder";
    
    public const string AdminUsersEditRole = AdminUsersBaseUrl + "/editrole";
    public const string AdminUsersAddRole = AdminUsersBaseUrl + "/addrole";
    public const string AdminUsersEdit = AdminUsersBaseUrl + "/edit";
    public const string AdminUsersList = AdminUsersBaseUrl + "/list";
    
    public const string AdminSettingsUpdateContentType = AdminSettingsBaseUrl + "/updatecontentype";
    public const string AdminSettingsCreateContentType = AdminSettingsBaseUrl + "/createcontentype";
    public const string AdminSettingsCopyContentType = AdminSettingsBaseUrl + "/copycontentype";
    
    public const string AdminSettingsAuditLog = AdminSettingsBaseUrl + "/auditlog";
    public const string AdminSettingsLanguages = AdminSettingsBaseUrl + "/languages";
    public const string AdminSettingsLanguageDictionaries = AdminSettingsBaseUrl + "/languagedictionaries";
    public const string AdminSettingsGlobalSettings = AdminSettingsBaseUrl + "/globalsettings";

    private const string AdminSeoBaseUrl = AdminBaseUrl + "/seo";
    public const string AdminSeoPageChecker = AdminSeoBaseUrl + "/pagechecker";
    public const string AdminSeoSitemaps = AdminSeoBaseUrl + "/sitemaps";
    public const string AdminSeoRedirects = AdminSeoBaseUrl + "/redirects";
        
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