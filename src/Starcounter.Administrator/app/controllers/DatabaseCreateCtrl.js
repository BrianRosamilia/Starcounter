/**
 * ----------------------------------------------------------------------------
 * Create Databases page Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('DatabaseCreateCtrl', ['$scope', '$log', '$location', '$anchorScroll', 'NoticeFactory', 'DatabaseService', 'UserMessageFactory', function ($scope, $log, $location, $anchorScroll, NoticeFactory, DatabaseService, UserMessageFactory) {

    // Database Default settings
    $scope.settings = null;


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
     * Create database
     * @param {settings} settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    $scope.createDatabase = function (settings, successCallback, errorCallback) {

        DatabaseService.createDatabase(settings, function () {
            // Success

            // TODO: Return the newly created database
            if (typeof (successCallback) == "function") {
                successCallback();
            }

        }, function (messageObject) {
            // Error
            if (typeof (errorCallback) == "function") {
                errorCallback(messageObject);
            }
        });

    }


    /**
     * Refresh database default settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    $scope.btnCreateDatabase = function (settings) {

        // Scroll top top
        $location.hash('top');
        $anchorScroll();

        $scope.verifySettings(settings, function (validationErrors) {
            // Success

            if (validationErrors.length == 0) {
                // No validation errors, goahead creating database

                $scope.createDatabase(settings, function () {
                    // Success
                    //$location.hash("");

                    // Navigate to database list if user has not navigated to another page
                    if ($location.path() == "/databaseCreate") {
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
     * Reset database settings
     */
    $scope.btnResetSettings = function () {
        $scope.refreshDatabaseDefaultSettings();
    }


    /**
     * Refresh database default settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    $scope.refreshDatabaseDefaultSettings = function () {

        DatabaseService.getDatabaseDefaultSettings(function (settings) {
            // Success

            settings.tempDirectory = settings.tempDirectory.replace("[DatabaseName]", settings.name);
            settings.imageDirectory = settings.imageDirectory.replace("[DatabaseName]", settings.name);
            settings.transactionLogDirectory = settings.transactionLogDirectory.replace("[DatabaseName]", settings.name);
            settings.dumpDirectory = settings.dumpDirectory.replace("[DatabaseName]", settings.name);

            $scope.settings = settings;

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


    // Init
    $scope.refreshDatabaseDefaultSettings();

}]);