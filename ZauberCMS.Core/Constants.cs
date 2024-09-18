namespace ZauberCMS.Core;

public static class Constants
{
    public const string SettingsConfigName = "Zauber";
    
    public static class Guids
    {
        public static readonly Guid ContentTypeSystemTabId = new("8282a912-408f-49dd-ab84-b19600d55aef");
        public static readonly Guid ContentTypeTreeRootId = new("bdff0832-1b3d-43b9-b546-1db5ade35b8b");
        public static readonly Guid ElementTypeTreeRootId = new("88e68720-fb1e-40b0-8735-35c5666235e1");
        public static readonly Guid AuditTreeRootId = new("4eb614c6-f399-4a50-8e36-6e3d1d716879");
        public static readonly Guid LanguageTreeRootId = new("b8f3582d-fa55-4448-88b6-6389413bfcd2");
        public static readonly Guid LanguageDictionaryTreeRootId = new("72bffd18-ec43-4e85-8569-596fcd60a14c");
        public static readonly Guid UsersTreeRootId = new("86000c41-647d-49d2-9de5-e481b7b4a1c1");
        public static readonly Guid RolesTreeRootId = new("b30e0814-d163-40d3-8913-65225cfa3652");
        public static readonly Guid RecycleBinRootId = new("97a0c80e-cbf2-4a70-a185-5a199f4cb8e2");
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

        public static class SectionNavGroups
        {
            public const string ContentNavGroup = "ContentNavGroup";
            public const string MediaNavGroup = "MediaNavGroup";
            public const string UsersNavGroup = "UsersNavGroup";
            public const string SettingsStructureNavGroup = "SettingsStructureNavGroup";
            public const string SettingsLanguagesNavGroup = "SettingsLanguagesNavGroup";
            public const string SettingsAdvancedNavGroup = "SettingsAdvancedNavGroup";
        }
    }
    
    public static class Urls
    {
        public const string AdminBaseUrl = "/admin";
        public const string AdminUsersBaseUrl = AdminBaseUrl + "/users";
        public const string AdminMediaBaseUrl = AdminBaseUrl + "/media";
        public const string AdminContentBaseUrl = AdminBaseUrl + "/content";
        public const string AdminSettingsBaseUrl = AdminBaseUrl + "/settings";
        public const string AdminFormsBaseUrl = AdminBaseUrl + "/forms";
        public const string RefreshSignIn = "/api/auth/refreshsignin";
        
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
    
    public static class Roles
    {
        public const string AdminRoleName = "Admin";
        public const string StandardRoleName = "Member";
    }
}