/**
 * ----------------------------------------------------------------------------
 * Sql page Controller
 * ----------------------------------------------------------------------------
 */
adminModule.controller('SqlCtrl', ['$scope', '$log', 'NoticeFactory', 'SqlService', 'DatabaseService', 'UserMessageFactory', function ($scope, $log, NoticeFactory, SqlService, DatabaseService, UserMessageFactory) {

    // List of databases
    $scope.databases = DatabaseService.databases;

    // Show/Hide progressbar
    $scope.isBusy = false;

    // Execute button title
    $scope.executeButtonTitle = function (isBusy) {
        if (isBusy) {
            return "Executing...";
        }
        else {
            return "Execute";
        }
    }

    //// TODO: Keep querystate
    //$scope.queryState = {
    //    selectedDatabaseName: null,
    //    sqlQuery: "",
    //    columns: [],
    //    rows: []
    //}

    /**
     * Execut query
     * @param {query} query
     * @param {databaseName} databaseName
     */
    $scope.btnExecute = function (query, databaseName) {

        NoticeFactory.ClearAll();

        if (!query) {
            // if this occure then the binding the the textarea failed..
            var message = "Failed to retrive the query text due to some binding issues. Refresh the page and try again.";
            NoticeFactory.ShowNotice({ type: 'error', msg: message, helpLink: null });
            return;
        }
        $scope.isBusy = true;

        // Execute query
        SqlService.executeQuery(query, databaseName, function (response) {

            $scope.isBusy = false;

            // Success
            $scope.queryState.columns = response.columns;
            $scope.queryState.rows = response.rows.rows;

            // Make all columns readonly
            for (var i = 0; i < $scope.queryState.columns.length ; i++) {
                $scope.queryState.columns[i].readOnly = true;
            }

            if (response.queryPlan) {
                $scope.queryState.queryPlan = response.queryPlan.replace(/\r\n/g, "<br>");  // Replace all occurrences of \r\n with the html tag <br>
                $scope.queryState.queryPlan = $scope.queryState.queryPlan.replace(/\t/g, "&emsp;");  // Replace all occurrences of \t with &emsp;
            }


        },
            function (messageObject) {
                // Error
                $scope.isBusy = false;

                if (messageObject.isError) {
                    UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
                }
                else {
                    NoticeFactory.ShowNotice({ type: 'error', msg: messageObject.message, helpLink: messageObject.helpLink });
                }

            });
    }


    // Init
    DatabaseService.refreshDatabases(function () {

        if ($scope.databases.length > 0) {
            $scope.queryState.selectedDatabaseName = $scope.databases[0].name;
        }

    }, function (messageObject) {
        // Error
        UserMessageFactory.showErrorMessage(messageObject.header, messageObject.message, messageObject.helpLink, messageObject.stackTrace);
    });



}]);