namespace ZauberCMS.Core;

public static class Constants
{
    public const string SettingsConfigName = "Zauber";
    
    public static class Guids
    {
        public static readonly Guid ContentTypeSystemTabId = new("8282a912-408f-49dd-ab84-b19600d55aef");
    }
    
    public static class Claims
    {
        public const string ProfileImage = "ProfileImage";
        public const string Md5Hash = "Md5Hash";
    }
    
    public static class ExtendedDataKeys
    {
        public const string PipelineAdditionalModel = "PipelineAdditionalModel";
        public const string TempUiMessages = "TempUiMessages";
        public const string IsExternalLogin = "IsExternalLogin";
        public const string NewEmailAddress = "NewEmailAddress";
    }
    
    public static class Assets
    {
        public const string DefaultEmailTemplate = "default.html";
    }

    public static class Identity
    {
        public const string LoginCallbackAction = "LoginCallback";
        public const string LinkLoginCallbackAction = "LinkLoginCallback";
    }
    
    public static class Sections
    {
        public const string ContentSection = "ContentSection";
        public const string MediaSection = "MediaSection";
        public const string SettingsSection = "SettingsSection";
        public const string UsersSection = "UsersSection";
        public const string FormsSection = "FormsSection";
        
        
    }
    
    public static class Urls
    {
        public const string AdminBaseUrl = "/admin";
        public const string AdminAccountBaseUrl = "/admin/account";
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
            public const string RefreshSignIn = "/account/refreshsignin";
            public const string UserProfile = "/account/manage/profile";
        }
    }
    
    public static class Roles
    {
        public const string AdminRoleName = "Admin";
        public const string StandardRoleName = "Member";
    }
}