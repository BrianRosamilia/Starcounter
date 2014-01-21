/**
 * ----------------------------------------------------------------------------
 * Version Check Directive
 * ----------------------------------------------------------------------------
 */
adminModule.directive("versionCheck", ['$http', '$log', 'VersionCheckService', 'NoticeFactory', 'UtilsFactory', function ($http, $log, VersionCheckService, NoticeFactory, UtilsFactory) {
    return {
        controller: function ($scope) {

            // Close version check alert box
            $scope.closeVersionNotice = function () {
                $scope.newVersion = null;

                if (typeof (Storage) !== "undefined") {
                    localStorage.lastVersionCheckUtcDate = (new Date()).toUTCString();
                }
                else {
                    // No web storage support..
                }

            };

            /**
             * Get latest starcounter version information
             * @param {successCallback} successCallback function
             * @param {errorCallback} errorCallback function
             */
            $scope.getLatestVersion = function (successCallback, errorCallback) {

                // {
                //    days:234,  
                //    version:"2.0.0.0",
                //    downloadUri:"http://downloads.starcounter.com/archive/NightlyBuilds/2.0.1290.3"
                // }
                VersionCheckService.getLatestVersionInfo(function (latestVersion) {
                    // Success

                    if (typeof (successCallback) == "function") {
                        successCallback(latestVersion);
                    }


                }, function (messageObject) {
                    // Error
                    if (typeof (errorCallback) == "function") {
                        errorCallback(messageObject);
                    }
                });


            }


            /**
             * Check if a version check is needed
             * @return {bool} true or false
             */
            $scope.needCheck = function () {

                if (typeof (Storage) !== "undefined") {

                    if (typeof (localStorage.lastVersionCheckUtcDate) !== "undefined") {

                        var now = new Date();
                        var now_utc = new Date(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), now.getUTCHours(), now.getUTCMinutes(), now.getUTCSeconds());

                        var lastCheck = new Date(localStorage.lastVersionCheckUtcDate);
                        var lastCheck_utc = new Date(lastCheck.getUTCFullYear(), lastCheck.getUTCMonth(), lastCheck.getUTCDate(), lastCheck.getUTCHours(), lastCheck.getUTCMinutes(), lastCheck.getUTCSeconds());

                        var days = Math.floor((now_utc.getTime() - lastCheck_utc.getTime()) / (1000 * 3600 * 24));

                        // Next check is 7 days after last check (until the user closes the notice
                        if (days >= 7) {
                            return true;
                        }
                    }
                    else {
                        return true;
                    }

                    return false;
                }

                // VersionCheck is disabled due to no web storage support
                return false;
            }


            /**
             * Check if a version check is needed
             * @return {bool} true or false
             */
            $scope.checkVersion = function () {

                if ($scope.needCheck()) {
                    $scope.getLatestVersion(function (latestVersion) {
                        // Success

                        if (latestVersion.days >= 7) {
                            $scope.newVersion = latestVersion;
                        }

                    }, function (messageObject) {
                        // Error

                        if (messageObject == null) {
                            // Silent error
                            return;
                        }

                        if (messageObject.isError) {
                            UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
                        }
                        else {
                            NoticeFactory.ShowNotice({ type: 'error', msg: messageObject.message, helpLink: messageObject.helpLink });
                        }
                    });
                }

            }

            // Init
            $scope.checkVersion();

        },
        restrict: "E",
        scope: {},
        //        template: "<alert ng-repeat='notice in notices' type='notice.type' close='closeNotice(notice)'>{{notice.msg}}<div data-ng-hide='notice.helpLink == null'><p>Help page: <a href='{{notice.helpLink}}' target='_blank'>{{notice.helpLink}}</a></p></div></alert>",
        template: "<div class='alert alert-info' data-ng-show='newVersion!=null'><button type='button' class='close' data-ng-click='closeVersionNotice()' data-dismiss='alert'>&times;</button><strong>New Version!</strong><p>A newer version of Starcounter is available.</p><a href='{{newVersion.downloadUri}}' target='_blank'>Download ({{newVersion.version}})</a></div>",
        link: function (scope) {
            //scope.notices = NoticeFactory.notises;
            //scope.closeNotice = function (notice) {
            //    NoticeFactory.CloseNotice(notice);
            //}

        }

    }

}]);


