/**
 * ----------------------------------------------------------------------------
 * Server Settings page Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('ServerSettingsCtrl', ['$scope', '$log', 'NoticeFactory', 'ServerService', 'UserMessageFactory', function ($scope, $log, NoticeFactory, ServerService, UserMessageFactory) {

    // Database Default settings
    $scope.model = ServerService.model;


    /**
     * Verify server settings
     * @param {settings} settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    $scope.verifySettings = function (settings, successCallback, errorCallback) {

        ServerService.verifyServerSettings(settings, function (validationErrors) {
            // Success
            if (typeof (successCallback) == "function") {
                successCallback(validationErrors);
            }

        }, function (messageObject) {
            // Error
            if (typeof (errorCallback) == "function") {
                errorCallback(messageObject);
            }

        });

    }




    /**
     * Save settings
     * @param {settings} settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    $scope.saveSettings = function (settings, successCallback, errorCallback) {

        ServerService.saveSettings(settings, function (messageObject) {
            // Success

            // TODO: Return the newly created database
            if (typeof (successCallback) == "function") {
                successCallback(messageObject);
            }

        }, function (messageObject) {
            // Error
            if (typeof (errorCallback) == "function") {
                errorCallback(messageObject);
            }
        });

    }


    /**
     * Reset server settings
     */
    $scope.btnResetSettings = function () {
        $scope.refreshServerSettings();
    }


    /**
     * Refresh server settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    $scope.refreshServerSettings = function () {

        ServerService.refreshServerSettings(function () {
            // Success

            $scope.myForm.$setPristine(); // This dosent work, the <select> breaks the pristine state :-(

        },
            function (messageObject) {
                // Error

                if (messageObject.isError) {
                    UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
                }
                else {
                    NoticeFactory.ShowNotice({ type: 'error', msg: messageObject.message, helpLink: messageObject.helpLink });
                }

            });
    }

    
    $scope.btnSaveSettings = function (settings) {

        $scope.verifySettings(settings, function (validationErrors) {
            // Success

            if (validationErrors.length == 0) {
                // No validation errors, goahead creating database

                $scope.saveSettings(settings, function (messageObject) {
                    // Success
                    //$location.hash("");

                    // Navigate to database list if user has not navigated to another page
                    //if ($location.path() == "/databaseCreate") {
                    //    $location.path("/databases");
                    //}
                    if (messageObject != null) {
                        NoticeFactory.ShowNotice({ type: messageObject.header, msg: messageObject.message, helpLink: messageObject.helpLink });
                    }


                }, function (messageObjectList) {
                    // Error
                    for (var i = 0; i < messageObjectList.length; i++) {
                        var messageObject = messageObjectList[i];
                        NoticeFactory.ShowNotice({ type: 'error', msg: messageObject.message, helpLink: messageObject.helpLink });
                    }

                });
            }
            else {

                // Show errors on screen
                for (var i = 0; i < validationErrors.length; i++) {
                    //$scope.alerts.push({ type: 'error', msg: validationErrors[i].message });
                    $scope.myForm[validationErrors[i].property].$setValidity("validationError", false);
                    var id = validationErrors[i].property;
                    var unregister = $scope.$watch("settings." + validationErrors[i].property, function (newValue, oldValue) {
                        if (newValue == oldValue) return;
                        $scope.myForm[id].$setValidity("validationError", true);
                        unregister();
                    }, false);

                }

            }

        }, function (messageObject) {
            // Error
            if (messageObject.isError) {
                UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
            }
            else {
                NoticeFactory.ShowNotice({ type: 'error', msg: messageObject.message, helpLink: messageObject.helpLink });
            }

        });



    }

    $scope.btnResetSettings = function () {
        $scope.refreshServerSettings();
    }

    // Init
    $scope.refreshServerSettings();

}]);