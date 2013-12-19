/**
 * ----------------------------------------------------------------------------
 * SQL Service
 * ----------------------------------------------------------------------------
 */
adminModule.service('SqlService', ['$http', '$log', 'UtilsFactory', 'JobFactory', function ($http, $log, UtilsFactory, JobFactory) {

    // List of databases
    // {
    //     "Name":"tracker",
    //     "Uri":"http://machine:1234/api/databases/mydatabase",
    //     "HostUri":"http://machine:1234/api/engines/mydatabase/db",
    //     "Running":true
    // }

    var self = this;


    /**
     * Execute query
     * @param {query} query
     * @param {database} database
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.executeQuery = function (query, databaseName, successCallback, errorCallback) {

        $log.info("Executing query");

        var errorHeader = "Failed to execute query";
        var uri = "/__" + databaseName + "/sql";

        //{
        // "columns":[{"title":"title","value":"Object","type":"Object"}],
        // "rows":{
        //      "rows":[{}]
        // },
        // "queryPlan":"some text",
        // "sqlException":{"message":"","helpLink":"","query":"","beginPosition":0,"endPosition":0,"scErrorCode":0,"token":"","stackTrace":""},
        // "hasSqlException":false,
        // "exception":{"message":"","helpLink":"","stackTrace":""},
        // "hasException":false
        //}
        $http.post(uri, query).then(function (response) {
            // Success

            if (response.data.hasOwnProperty("hasSqlException") && response.data.hasSqlException) {
                // Show message
                //$scope.alerts.push({ type: 'error', msg: response.sqlException.message, helpLink: response.sqlException.helpLink });
                if (typeof (errorCallback) == "function") {
                    var messageObject = UtilsFactory.createMessage(errorHeader, response.data.sqlException.message, response.data.sqlException.helpLink);
                    errorCallback(messageObject);
                }
                return;
            }

            if (response.data.hasOwnProperty("hasException") && response.data.hasException) {
                //$scope.showServerError(response.exception.message, response.exception.helpLink, response.exception.stackTrace);
                if (typeof (errorCallback) == "function") {
                    var messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.exception.message, response.data.exception.helpLink, response.data.exception.stackTrace);
                    errorCallback(messageObject);
                }
                return;
            }

            // Validate response
            if (response.data.hasOwnProperty("rows") == true && response.data.hasOwnProperty("columns") == true ) {
                $log.info("rows (" + response.data.rows.rows.length + ") successfully retrived");
                $log.info("columns (" + response.data.columns.length + ") successfully retrived");
                if (typeof (successCallback) == "function") {
                    successCallback(response.data);
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


}]);
