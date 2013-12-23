/**
 * ----------------------------------------------------------------------------
 * Gateway Service
 * ----------------------------------------------------------------------------
 */
adminModule.service('GatewayService', ['$http', '$sce', '$log', 'UtilsFactory', function ($http, $sce,$log, UtilsFactory) {

    // Gateway model
    // {
    //    statistics : "<p>html text</p>";
    // }
    this.model = {};

    var self = this;

    /**
     * Get Gateway Statistics
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.getGatewayStatistics = function (successCallback, errorCallback) {

        $log.info("Retriving Gateway statistics");

        var errorHeader = "Failed to retrive Gateway statistics";
        var uri = "/gwstats";

        $http.get(uri).then(function (response) {
            // Success
            $log.info("Gateway statistics successfully retrived");
            if (typeof (successCallback) == "function") {
                successCallback(response.data);
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
                if (response.data.hasOwnProperty("Text") == true) {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.Text, response.data.Helplink, null);
                } else {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data, null, null);
                }
            }

            $log.error(errorHeader, response);

            if (typeof (errorCallback) == "function") {
                errorCallback(messageObject);
            }


        });


    }


    /**
     * Refresh Gateway Statistics
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.refreshGatewayStatistics = function (successCallback, errorCallback) {

        this.getGatewayStatistics(function (statistics) {
            // Success
      
            self.model.statistics = $sce.trustAsHtml(statistics);

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


}]);
