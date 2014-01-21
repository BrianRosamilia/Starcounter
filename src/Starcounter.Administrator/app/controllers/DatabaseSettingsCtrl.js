/**
 * ----------------------------------------------------------------------------
 * Databases Settings page Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('DatabaseSettingsCtrl', ['$scope', '$log', '$location', '$routeParams', '$anchorScroll', 'NoticeFactory', 'DatabaseService', 'UserMessageFactory', function ($scope, $log, $location, $routeParams, $anchorScroll, NoticeFactory, DatabaseService, UserMessageFactory) {


    // Model
    $scope.model = {
        database: null,
        settings: null
    }


    /**
     * Verify database settings
     * @param {settings} settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    $scope.verifySettings = function (settings, successCallback, errorCallback) {

        DatabaseService.verifyDatabaseSettings(settings, function (validationErrors) {
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
     * @param {database} database
     * @param {settings} settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    $scope.saveSettings = function (database, settings, successCallback, errorCallback) {

        DatabaseService.saveDatabaseSettings(database, settings, function (messageObject) {
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
     * Refresh database settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    $scope.refreshSettings = function () {

        DatabaseService.getDatabaseSettings($scope.model.database, function (settings) {
            // Success

            $scope.model.settings = settings;
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


    /**
     * Save settings
     */
    $scope.btnSaveSettings = function (database, settings) {

        $scope.verifySettings(settings, function (validationErrors) {
            // Success

            if (validationErrors.length == 0) {
                // No validation errors, goahead creating database

                $scope.saveSettings(database, settings, function (messageObject) {
                    // Success
                    //$location.hash("");

                    // Navigate to database list if user has not navigated to another page
                    //if ($location.path() == "/databaseCreate") {
                    //    $location.path("/databases");
                    //}
                    if (messageObject != null) {
                        NoticeFactory.ShowNotice({ type: messageObject.header, msg: messageObject.message, helpLink: messageObject.helpLink });
                    }

                    $scope.myForm.$setPristine(); 

                    // Navigate to Executable list if user has not navigated to another page
                    if ($location.path() == "/databases/" + database.name + "/settings") {
                        $location.path("/databases");
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


    /**
     * Reset server settings
     */
    $scope.btnResetSettings = function () {
        $scope.refreshSettings();
    }

    // Init
    // Refresh databases list
    DatabaseService.refreshDatabases(
        function () {
            // Success
            $scope.model.database = DatabaseService.getDatabase($routeParams.name);
            $scope.refreshSettings();
        },
        function (messageObject) {
            // Error
            UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
        });


}]);