/**
 * ----------------------------------------------------------------------------
 * Server Service
 * ----------------------------------------------------------------------------
 */
adminModule.service('ServerService', ['$http', '$log', '$rootScope', 'UtilsFactory', 'JobFactory', function ($http, $log, $rootScope, UtilsFactory, JobFactory) {

    // Settings
    // {
    //     name:"default",
    //     httpPort:8080,
    //     version:"2.0.0.0"
    // }
    //this.settings = { name:"test",httpPort:1234,"Version":"2.3.4.5"};


    this.model = {
        settings: null
    }

    var self = this;


    /**
     * Get Server settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.getServerSettings = function (successCallback, errorCallback) {

        $log.info("Retriving server settings");

        var errorHeader = "Failed to retrive server settings";
        var uri = "/api/admin/servers/personal/settings";

        // Response
        // {
        //     name:"default",
        //     httpPort:8080,
        //     version:"2.0.0.0"
        // }
        $http.get(uri).then(function (response) {
            // success handler

            // Validate response
            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("settings") == true) {
                $log.info("Server settings successfully retrived");
                if (typeof (successCallback) == "function") {
                    successCallback(response.data.settings);
                }
            }
            else {
                // Error
                $log.error(errorHeader, response);

                if (typeof (errorCallback) == "function") {
                    var messageObject = UtilsFactory.createErrorMessage(errorHeader, "Invalid response content", null, null);
                    errorCallback(messageObject);
                }
            }

        }, function (response) {

            // Error
            var messageObject;

            if (response instanceof SyntaxError) {
                messageObject = UtilsFactory.createErrorMessage(errorHeader, response.message, null, response.stack);
            }
            else if (response.status == 500) {
                // 500 Server Error
                errorHeader = "Internal Server Error";
                if (response.data.hasOwnProperty("Text") == true) {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.Text, response.data.Helplink, null);
                } else {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data, null, null);
                }
            }
            else {
                // Unhandle Error
                messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.Text, response.data.Helplink, null);
            }

            $log.error(errorHeader, response);

            if (typeof (errorCallback) == "function") {
                errorCallback(messageObject);
            }

        });


    }


    /**
     * Refresh Server Settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.refreshServerSettings = function (successCallback, errorCallback) {

        this.getServerSettings(function (settings) {
            // Success

            // TODO: Update current properties with new values
            //       instead of replacing the settings object

            // Clear database list
            self.model.settings = settings

            //$rootScope.$apply(
            //    function() {
            //        self.settings = settings
            //    }
            //);
            if (typeof (successCallback) == "function") {
                successCallback();
            }

        }, function (response) {
            // Error

            if (typeof (errorCallback) == "function") {
                errorCallback(response);
            }

        });
    }


    /**
     * Verify server settings
     * @param {settings} Settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.verifyServerSettings = function (settings, successCallback, errorCallback) {

        $log.info("Verifying server settings");

        var errorHeader = "Failed to verify server settings";
        var job = { message: "Verifying server settings" };
        JobFactory.AddJob(job);

        $http.post('/api/admin/verify/serverproperties', settings).then(function (response) {
            // success handler
            JobFactory.RemoveJob(job);

            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("validationErrors") == true) {

                $log.info("Server settings successfully verified");
                if (typeof (successCallback) == "function") {
                    successCallback(response.data.validationErrors);
                }

            }
            else {

                if (typeof (errorCallback) == "function") {
                    var messageObject = UtilsFactory.createErrorMessage(errorHeader, "Invalid response content", null, null);
                    errorCallback(messageObject);
                }

            }

        }, function (response) {
            // Error
            JobFactory.RemoveJob(job);
            var messageObject;

            if (response instanceof SyntaxError) {
                messageObject = UtilsFactory.createErrorMessage(errorHeader, response.message, null, response.stack);
            }
            else if (response.status == 403) {
                // 403 forbidden
                messageObject = UtilsFactory.createMessage(errorHeader, response.data.Text, response.data.Helplink);
            }
            else if (response.status == 500) {
                // 500 Server Error
                errorHeader = "Internal Server Error";
                if (response.data.hasOwnProperty("Text") == true) {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.Text, response.data.Helplink, null);
                } else {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data, null, null);
                }
            }
            else {
                // Unhandle Error
                messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.Text, response.data.Helplink, null);
            }

            $log.error(errorHeader, response);

            if (typeof (errorCallback) == "function") {
                errorCallback(messageObject);
            }


        });

    }


    /**
     * Save server settings
     * @param {settings} settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.saveSettings = function (settings, successCallback, errorCallback) {

        $log.info("Saving server settings");

        var errorHeader = "Failed to save server settings";

        var job = { message: "Saving server settings" };
        JobFactory.AddJob(job);

        $http.put('/api/admin/servers/personal/settings', settings).then(function (response) {
            // success handler
            JobFactory.RemoveJob(job);



            if (response.data.hasOwnProperty("errors") == true) {
                var messageObjectList = [];

                for (var i = 0; i < response.data.errors.length; i++) {
                    messageObject = UtilsFactory.createMessage(errorHeader, response.data.errors[i].message, response.data.errors[i].helplink);
                    messageObjectList.push(messageObject);
                }

                if (errorCallback != null) {
                    errorCallback(messageObjectList);
                }

            }
            else {

                var messageObject = null;
                if (response.data.hasOwnProperty("message") == true) {

                    messageObject = UtilsFactory.createMessage("success", response.data.message, null);

                    //$scope.alerts.push({ type: 'success', msg: response.data.message });
                }


                if (successCallback != null) {
                    // TODO: Return the new settings
                    successCallback(messageObject);
                }

            }

        }, function (response) {

            // Error
            JobFactory.RemoveJob(job);
            var messageObject;

            if (response instanceof SyntaxError) {
                messageObject = UtilsFactory.createErrorMessage(errorHeader, response.message, null, response.stack);
            }
            else if (response.status == 403) {
                // 403 forbidden
                messageObject = UtilsFactory.createMessage(errorHeader, response.data.Text, response.data.Helplink);
            }
                //else if (response.status == 422) {
                //    // 422 Unprocessable Entity (WebDAV; RFC 4918)
                //    // The request was well-formed but was unable to be followed due to semantic errors
                //    messageObject = UtilsFactory.createMessage(errorHeader, response.data.Text, response.data.Helplink);
                //}
            else if (response.status == 500) {
                // 500 Server Error
                errorHeader = "Internal Server Error";
                if (response.data.hasOwnProperty("Text") == true) {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.Text, response.data.Helplink, null);
                } else {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data, null, null);
                }
            }
            else {
                // Unhandle Error
                messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.Text, response.data.Helplink, null);
            }

            $log.error(errorHeader, response);

            if (typeof (errorCallback) == "function") {
                errorCallback(messageObject);
            }

        });

    }

}]);
