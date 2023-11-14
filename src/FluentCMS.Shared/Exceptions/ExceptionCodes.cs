﻿namespace FluentCMS;

public class ExceptionCodes
{
    #region General

    public const string GeneralPermissionDenied = "General.PermissionDenied";

    #endregion

    #region Host

    public const string HostNotFound = "Host.NotFound";
    public const string HostAlreadyInitialized = "Host.AlreadyInitialized";
    public const string HostUnableToCreate = "Host.UnableToCreate";
    public const string HostUnableToUpdate = "Host.UnableToUpdate";
    public const string HostUnableToDelete = "Host.UnableToDelete";
    public const string HostAtLeastOneSuperUser = "Host.AtLeastOneSuperUser";
    public const string HostUnableToRemoveYourself = "Host.UnableToRemoveYourself";

    #endregion

    #region User

    public const string UserNotFound = "User.NotFound";
    public const string UserLoginFailed = "User.LoginFailed";
    public const string UserChangePasswordFailed = "User.ChangePasswordFailed";
    public const string UserTokenGenerationFailed = "User.TokenGenerationFailed";

    #endregion

    #region Page

    public const string PageNotAuthorizedToViewThisPage = "Page.NotAuthorizedToViewThisPage";
    public const string PageNotAuthorizedToCreate = "Page.PageNotAuthorizedToCreate";
    public const string PageNotAuthorizedToDeleteOrUpdate = "Page.NotAuthorizedToDeleteOrUpdate";
    public const string PageUnableToCreate = "Page.UnableToCreated";
    public const string PageUnableToUpdate = "Page.UnableToUpdate";
    public const string PageUnableToDelete = "Page.UnableToDelete";
    public const string PagePathMustBeUnique = "Page.PathMustBeUnique";
    public const string PageNotFound = "Page.NotFound";

    #endregion
}
