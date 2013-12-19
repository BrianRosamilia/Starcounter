/**
 * ----------------------------------------------------------------------------
 * sc.sqlquery
 * ----------------------------------------------------------------------------
 */
var sqlQueryModule = angular.module('sc.sqlquery', ['sc.sqlquery.service', 'sc.sqlquery.directive'], function ($routeProvider) {

    $routeProvider.when('/sql', {
        templateUrl: '/partials/sql.html',
        controller: 'SqlQueryCtrl'
    });

}).value('ui.config', {
    codemirror: {
        mode: 'text/x-mysql',
        lineNumbers: true,
        matchBrackets: true
    }
});

/**
 * Sql Query Controller
 */
sqlQueryModule.controller('SqlQueryCtrl', ['$scope', 'SqlQuery', '$rootScope', function ($scope, SqlQuery, $rootScope) {

    $scope.alerts.length = 0;
    $scope.isBusy = false;

    $scope.executeButtonTitle = function () {
        if ($scope.isBusy) {
            return "Executing...";
        }
        else {
            return "Execute";
        }
    }

    $scope.executeQuery = function () {

        if (!$scope.queryState.sqlQuery) {
            // if this occure then the binding the the textarea failed..
            var message = "Failed to retrive the query text due to some binding issues. Refresh the page and try again.";
            $scope.alerts.push({ type: 'error', msg: message });
            return;
        }
        $scope.isBusy = true;

        $scope.queryState.columns = [];
        $scope.queryState.rows = [];

        SqlQuery.send({ name: $scope.queryState.selectedDatabaseName }, $scope.queryState.sqlQuery, function (response, headers) {

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

            if (response.hasSqlException) {
                // Show message
                $scope.alerts.push({ type: 'error', msg: response.sqlException.message, helpLink: response.sqlException.helpLink });
            }

            if (response.hasException) {
                $scope.showServerError(response.exception.message, response.exception.helpLink, response.exception.stackTrace);
            }

        }, function (response) {
            // Error
            $scope.isBusy = false;
            if (response instanceof SyntaxError) {
                $scope.showServerError(response.message, null, response.stack);
            }
            else {

                if (response.status == 404) {
                    // 404	Not Found
                    var message = "Failed to execute the query on " + $scope.selectedDatabaseName + " database, Caused by a missing or not started executable.";
                    $scope.alerts.push({ type: 'error', msg: message });

                }
                else {
                    $scope._handleErrorReponse(response);
                }

            }
        });
    }

    // User clicked the "Execute" button
    $scope.btnExecute = function () {
        $scope.alerts.length = 0;
        $scope.executeQuery();
    }

    // Init
 
    if ($scope.queryState.selectedDatabaseName == "") {

        $scope._RefreshDatabases(function () {
            // success
            for (var i = 0 ; i < $scope.databases.length ; i++) {
                if ($scope.databases[i].running == true) {
                    $scope.queryState.selectedDatabaseName = $scope.databases[0].name;
                    break;
                }
            }

        });
    }

}]);


/**
 * sc.sqlquery.service
 */
angular.module('sc.sqlquery.service', ['ngResource'], function ($provide) {

    // OLDWAY TO EXECUT THE SQL QUERY
    //$provide.factory('SqlQuery', function ($resource) {
    //    return $resource('/api/admin/databases/:name/sql', { name: '@name' }, {
    //        send: { method: 'POST', isArray: false }    // We need to override this (the return type is not an array)
    //    });
    //});

    $provide.factory('SqlQuery', function ($resource) {
        return $resource('/__:name/sql', { name: '@name' }, {
            send: { method: 'POST', isArray: false }    // We need to override this (the return type is not an array)
        });
    });


});


/**
 * sc.sqlquery.directive
 */
angular.module('sc.sqlquery.directive', ['ui.config']);

/**
 * ----------------------------------------------------------------------------
 * scadmin
 * ----------------------------------------------------------------------------
 */
var adminModule = angular.module('scadmin', ['sc.sqlquery', 'ui', 'ui.bootstrap', 'uiHandsontable'], function ($routeProvider) {

    // List of all Databases
    $routeProvider.when('/databases', {
        templateUrl: '/partials/databases.html',
        controller: 'DatabasesCtrl'
    });

    // One Database
    $routeProvider.when('/databases/:name', {
        templateUrl: '/partials/database.html',
        controller: 'DatabaseCtrl'
    });

    // Edit database settings
    $routeProvider.when('/databases/:name/edit', {
        templateUrl: '/partials/database_edit.html',
        controller: 'DatabaseEditCtrl'
    });

    // Create database
    $routeProvider.when('/database_create', {
        templateUrl: '/partials/database_create.html',
        controller: 'DatabaseCreateCtrl'
    })

    // List of all Exectuables
    $routeProvider.when('/executables', {
        templateUrl: '/partials/executables.html',
        controller: 'ExecutablesCtrl'
    });

    // List of all Exectuables
    $routeProvider.when('/executable_start', {
        templateUrl: '/partials/executable_start.html',
        controller: 'ExecutableStartCtrl'
    });

    $routeProvider.when('/server_edit', {
        templateUrl: '/partials/server_edit.html',
        controller: 'ServerEditCtrl'
    });

    $routeProvider.when('/log', {
        templateUrl: '/partials/log.html',
        controller: 'LogCtrl',
        resolve: {
            redirect: function ($route, $location) {
                if (jQuery.isEmptyObject($location.search())) {
                    // Set default search filter
                    $location.search({ "debug": false, "notice": false, "warning": true, "error": true, "source": "" });
                }
            }
        }
    });

    $routeProvider.when('/gateway', {
        templateUrl: '/partials/gateway.html',
        controller: 'GatewayCtrl'
    });

    $routeProvider.otherwise({ redirectTo: '/executables' });

    //$locationProvider.html5Mode(true);

});

/**
 * Head Controller
 */
adminModule.controller('HeadCtrl', ['$scope', '$http', '$location', '$dialog', '$rootScope', function ($scope, $http, $location, $dialog, $rootScope) {

    $scope.alerts = [];
    $scope.jobs = [];           // { message:"default" }
    $scope.databases = [];      // { "name": "foo", "uri": "http://example.com/api/databases/foo", "engineUri":"http://example.com/api/engines/foo", "codeHostProcessUri":"http://example.com/api/engines/foo/host",    "databaseProcessUri":"http://example.com/api/engines/foo/db", "running": false}
    $scope.executables = [];    // { path:"c:\tmp\some.exe", databaseName:"default" }

    $scope.newVersion = null;

    $scope.queryState = {
        selectedDatabaseName: "",
        sqlQuery: "",
        columns: [],
        rows: [],
        queryPlan: ""
    };

    $rootScope.$on("$routeChangeError", function (event, current, pervious, refection) {
        $scope.showNetworkDownError();
    });

    // Handles the active navbar item
    $scope.isActiveUrl = function (path) {
        return $location.path() == path;
    }

    // Close alert box
    $scope.closeAlert = function (index) {
        $scope.alerts.splice(index, 1);
    };

    // Close version check alert box
    $scope.closeVersionNotice = function () {
        $scope.newVersion = null;

        if (typeof (Storage) !== "undefined") {
            localStorage.lastVersionCheckUtcDate = (new Date()).toUTCString();
        }
        else {
            // No web storage support.. (this wont be called)
        }

    };

    // Show Client Error dialog
    $scope.showClientError = function (message, helpLink, stackTrace) {
        $scope.showError("Client Error", message, helpLink, stackTrace);
    }

    // Show Server Error dialog
    $scope.showServerError = function (message, helpLink, stackTrace) {
        $scope.showError("Server Error", message, helpLink, stackTrace);
    }

    // Show Network down..
    $scope.showNetworkDownError = function (helpLink) {
        $scope.alerts.push({ type: 'error', msg: "The server is not responding or is not reachable.", helpLink: helpLink });
    }

    // Show Exception dialog
    $scope.showError = function (title, message, helpLink, stackTrace) {

        $scope.opts = {
            backdrop: true,
            keyboard: true,
            backdropClick: true,
            templateUrl: "partials/error.html",
            controller: 'DialogCtrl',
            data: { header: title, message: message, stackTrace: stackTrace, helpLink: helpLink }
        };

        var d = $dialog.dialog($scope.opts);
        d.open();
    }

    $scope.addJob = function (job) {
        $scope.jobs.push(job);
        return job;
    }

    $scope.removeJob = function (job) {

        // Remove job
        var index = $scope.jobs.indexOf(job);
        if (index != -1) {
            $scope.jobs.splice(index, 1);
        }
        return job;
    }

    // Get all databases
    $scope._GetDatabases = function (readyCallback) {

        $http.get('/api/admin/databases').then(function (response) {

            // Example JSON response 
            //-----------------------
            //{
            //    "Databases": [
            //        {
            //            "name": "foo",
            //            "uri": "http://example.com/api/databases/foo",
            //            "engineUri":"http://example.com/api/engines/foo",
            //            "codeHostProcessUri":"http://example.com/api/engines/foo/host",
            //            "databaseProcessUri":"http://example.com/api/engines/foo/db",
            //            "running": false
            //        },
            //        {
            //            "name": "bar",
            //            "uri": "http://example.com/api/databases/bar",
            //            "engineUri":"http://example.com/api/engines/bar",
            //            "codeHostProcessUri":"http://example.com/api/engines/bar/host",
            //            "databaseProcessUri":"http://example.com/api/engines/bar/db",
            //            "running": false
            //        },
            //    ]
            //}

            // success handler
            if (response.data.hasOwnProperty("Databases") == true) {
                var remoteDatabaseList = response.data.Databases;

                // Fix the engineUri to a relative path
                // Example: "http://headsutv19:8181/api/engines/default" to "/api/engines/default"
                for (var i = 0; i < remoteDatabaseList.length ; i++) {
                    remoteDatabaseList[i].engineUri = toRelativePath(remoteDatabaseList[i].engineUri);
                }

                if (readyCallback != null) {
                    readyCallback(remoteDatabaseList);
                }
            }
            else {
                $scope.showClientError("Unknown server response", null, "._GetDatabases() " + JSON.stringify(response.data));
                if (readyCallback != null) {
                    readyCallback([]);
                }
            }

        }, function (response) {
            // error handler
            $scope._handleErrorReponse(response);
            if (readyCallback != null) {
                readyCallback([]);
            }
        });

    }

    // Refresh database list
    $scope._RefreshDatabases = function (readyCallback) {

        $scope._GetDatabases(function (databases) {

            $scope.databases.length = 0;
            $scope.databases = databases;

            if (readyCallback != null) {
                readyCallback();
            }
        });
    }

    // Get Execuables from all engines
    $scope._GetExecutables = function (readyCallback) {

        $http.get('/api/admin/executables').then(function (response) {

            // Example JSON response 
            //-----------------------
            //{
            //  "Executables":[
            //      {
            //          "path":"c:\path\to\executable\foo.exe",
            //          "uri":"http://example.com/foo.exe-12345",
            //          "databaseName":"default"
            //      }
            //  ]
            //}

            // success handler
            if (response.data.hasOwnProperty("Executables") == true) {

                if (readyCallback != null) {
                    readyCallback(response.data.Executables);
                }

            }
            else {
                $scope.showClientError("Unknown server response", null, "._GetExecutables() " + JSON.stringify(response.data));
                if (readyCallback != null) {
                    readyCallback([]);
                }
            }

        }, function (response) {
            // error handler
            $scope._handleErrorReponse(response);
            if (readyCallback != null) {
                readyCallback([]);
            }
        });


    }

    // Refresh executable list
    $scope._RefreshExecutables = function (readyCallback) {

        $scope._GetExecutables(function (executables) {

            $scope.executables.length = 0;
            $scope.executables = executables;

            if (readyCallback != null) {
                readyCallback();
            }
        });

    }

    // Get server settings
    $scope._GetServerSettings = function (name, successCallback, errorCallback) {

        $http.get('/api/admin/servers/' + name + '/settings').then(function (response) {
            // success handler

            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("settings") == true) {

                if (successCallback != null) {
                    successCallback(response.data.settings);
                }
            }
            else {
                $scope.showClientError("Unknown server response", null, "._GetServerSettings() " + JSON.stringify(response.data));
            }

        }, function (response) {
            // error handler
            $scope._handleErrorReponse(response);
            if (errorCallback != null) {
                errorCallback(null);
            }

        });

    }

    // Verify database properties
    $scope._VerifyServerProperties = function (properties, successCallback, errorCallback) {

        $http.post('/api/admin/verify/serverproperties', properties).then(function (response) {
            // success handler

            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("validationErrors") == true) {

                if (successCallback != null) {
                    successCallback(response.data.validationErrors);
                }

            }
            else {
                $scope.showClientError("Unknown server response", null, "._VerifyServerProperties() " + JSON.stringify(response));
                if (errorCallback != null) {
                    errorCallback();
                }
            }

        }, function (response) {
            // error handler
            $scope._handleErrorReponse(response);

            if (errorCallback != null) {
                errorCallback();
            }

        });

    }

    // Save database information
    $scope._SaveServerSettings = function (server, successCallback, errorCallback) {

        $http.put('/api/admin/servers/' + server.name + '/settings', server).then(function (response) {
            // success handler

            if (response.data.hasOwnProperty("message") == true) {
                $scope.alerts.push({ type: 'success', msg: response.data.message });
            }

            if (response.data.hasOwnProperty("settings") == true) {
                if (successCallback != null) {
                    successCallback(response.data.settings);
                }
            }
            else {
                $scope.showClientError("Unknown server response", null, "._SaveServerSettings() " + JSON.stringify(response.data));
                if (errorCallback != null) {
                    errorCallback();
                }
            }

        }, function (response) {
            // error handler
            $scope._handleErrorReponse(response);
            if (errorCallback != null) {
                errorCallback();
            }
        });

    }

    // Handle errors (Show user dialog/message)
    $scope._handleErrorReponse = function (response) {

        // error handler
        if (response instanceof SyntaxError) {
            $scope.showServerError(response.message, null, response.stack);
        }
        else if (response.status == 404) {
            // 404 Not Found

            if (response.data.hasOwnProperty("Text") == true) {
                $scope.showServerError(response.data.Text, response.data.Helplink);
            }
            else if (response.data.hasOwnProperty("message") == true) {
                $scope.showServerError(response.data.message, response.data.helpLink, response.data.stackTrace);
            }
            else {

                if (response.data == "") {
                    $scope.showServerError("404 Not found", null, null);
                }
                else {
                    $scope.showServerError(response.data, null, null);
                }
            }

        }
        else if (response.status == 405) {
            // 405 Method Not Allowed
            if (response.data.hasOwnProperty("Allow") == true) {
                $scope.showClientError("Allowed methods are " + response.data.Allow, response.data.Helplink);
            }
            else {
                $scope.showClientError(response.data, null, null);
            }

        }
        else if (response.status == 409) {
            // 409 Conflict
            if (response.data.hasOwnProperty("Text") == true) {
                $scope.showServerError(response.data.Text, response.data.Helplink);
            }
            else {
                $scope.showServerError(response.data, null, null);
            }

        }
        else if (response.status == 422) {
            // 422 The request was well-formed but was unable to be followed due to semantic errors

            if (response.data.hasOwnProperty("Text") == true) {
                $scope.showServerError(response.data.Text, response.data.Helplink);
            }
            else {
                $scope.showServerError(response.data, null, null);
            }

        }

        else if (response.status == 500) {

            if (response.data.hasOwnProperty("exception") == true) {
                $scope.showServerError(response.data.exception.message, response.data.exception.helpLink, response.data.exception.stackTrace);
            }
            else {
                if (response.data.hasOwnProperty("message") == true) {
                    $scope.showServerError(response.data.message, response.data.helpLink, response.data.stackTrace);
                }
                else if (response.data.hasOwnProperty("Text") == true) {
                    $scope.showServerError(response.data.Text, response.data.Helplink);
                }
                else {
                    $scope.showServerError(response.data, null, null);
                }
            }

        }
        else if (response.status == 0) {
            $scope.showNetworkDownError();
            // TODO:
            //  status==0 for a failed XmlHttpRequest should be considered an undefined error.

        }
        else {
            $scope.showClientError("Unhandled http statuscode " + response.status, null, null);
        }

    }

    // Stop the executable's engine code host (All executables running in the database will be stopped)
    $scope._StopAllExecutables = function (database, readyCallback) {

        $http.delete('/api/engines/' + database.name + '/host').then(function (response) {
            // success handler

            if (response.hasOwnProperty("data") == true) {

                if (readyCallback != null) {
                    readyCallback();
                }
            }
            else {
                $scope.showClientError("Unknown server response", null, "._StopAllExecutables() " + JSON.stringify(response.data));
            }


        }, function (response) {
            // error handler
            $scope._handleErrorReponse(response);
            if (readyCallback != null) {
                readyCallback();
            }

        });


    }

    // Get engine
    $scope._GetEngine = function (database, successCallback, errorCallback) {

        var uri = toRelativePath(database.engineUri);

        $http.get(uri).then(function (response) {
            // success 
            if (successCallback != null) {
                successCallback(response);
            }

        }, function (response) {
            // error
            if (errorCallback != null) {
                errorCallback(response);
            }

        });
    }

    // Start engine
    $scope._StartEngine = function (engineData, successCallback, errorCallback) {

        $http.post('/api/engines', engineData).then(function (response) {
            // success 

            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }
            });


        }, function (response) {
            // error
            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }
            });



        });
    }

    // Stop engine
    $scope._StopEngine = function (database, successCallback, errorCallback) {

        var uri = toRelativePath(database.codeHostProcessUri);

        // engine.CodeHostProcess.Uri
        $http.delete(uri).then(function (response) {
            // success 

            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }
            });


        }, function (response) {
            // error
            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }
            });



        });
    }

    // Start Executable
    $scope._StartExecutable = function (databaseName, data, successCallback, errorCallback) {

        // Start Executable
        var bodyData = { Path: data.file, StartedBy: data.startedBy, Arguments: data.arguments };

        $http.post('/api/engines/' + databaseName + '/executables', bodyData).then(function (response) {
            // success

            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }
            });

        }, function (response) {
            // error

            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }
            });


        });


    }

    // Stop Executable
    $scope._StopExecutable = function (executable, successCallback, errorCallback) {

        var url = toRelativePath(executable.uri);
        $http.delete(url).then(function (response) {
            // success
            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }
            });

        }, function (response) {
            // error
            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }
            });

        });
    }


    // Stop Codehost
    $scope._StopCodeHost = function (database, successCallback, errorCallback) {

        var url = toRelativePath(database.codeHostProcessUri);
        $http.delete(url).then(function (response) {

            // success
            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }
            });

        }, function (response) {
            // error
            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }
            });

        });
    }

    // Execute Executable
    $scope._ExecuteExecutable = function (executable, restart, job, successCallback, errorCallback) {

        // 1 Get engine
        // TODO: Create database
        // 2. Start engine if not started
        // 3. Start database
        // 4. Stop Executable (if Restart==true)
        // 5. Start Executable


        // Get database
        var database = $scope.GetDatabase(executable.databaseName);
        if (database == null) {
            $scope.showClientError("Failed to get database " + executable.databaseName, null, "._StartExecutable()");
            if (errorCallback != null) {
                errorCallback();
            }
            return;
        }

        $scope._AssureEngineIsRunning(database, job, function (response) {
            // Success

            var isExeRunning = false;
            for (var i = 0; i < $scope.executables.length; i++) {
                if (executable.path == $scope.executables[i].path &&
                    executable.databaseName == $scope.executables[i].databaseName) {
                    isExeRunning = true;
                    break;
                }
            }

            if (isExeRunning == false) {

                $scope._DoStartExecutable(executable, job, function () {
                    // Success

                    if (successCallback != null) {
                        successCallback();
                    }


                }, function (response) {
                    // Error
                    if (errorCallback != null) {
                        errorCallback(response);
                    }

                });

            }
            else {

                // Executable is running

                if (restart == false) {
                    // Executable is already running in database, try restart.
                    $scope.alerts.push({ type: 'info', msg: "Executable is already running in database " + database.name });
                    if (errorCallback != null) {
                        errorCallback();
                    }
                    return;
                }
                else {


                    // Restarting database
                    var fellowCount = $scope.executables.length - 1;
                    var status = "Restarting database " + database.name;
                    if (fellowCount > 0) {
                        status += " (and " + fellowCount + " other executable" + (fellowCount > 1 ? "s" : "") + ")";
                    }

                    job.message = status;

                    // Stop executable
                    $scope._StopExecutable(executable, function () {
                        // Success

                        $scope._DoStartExecutable(executable, job, function () {
                            // Success
                            if (successCallback != null) {
                                successCallback();
                            }

                        }, function (response) {
                            // Error

                            if (errorCallback != null) {
                                errorCallback(response);
                            }

                        });


                    }, function (response) {
                        // Error

                        if (response.status == 404) {
                            // 404 Not Found (Executable not running)
                            $scope._DoStartExecutable(executable, job, function () {
                                // Success
                                if (successCallback != null) {
                                    successCallback();
                                }

                            }, function (response) {
                                // Error

                                if (errorCallback != null) {
                                    errorCallback(response);
                                }

                            });
                        } else {

                            if (errorCallback != null) {
                                errorCallback(response);
                            }

                        }

                    });

                }

            }


        }, function (response) {
            // Error assuring engine (Could not start engine or creating database)

            $scope._handleErrorReponse(response);

            if (errorCallback != null) {
                errorCallback(response);
            }
        });


    }


    $scope._DoStartExecutable = function (executable, job, successCallback, errorCallback) {

        job.message = "Starting executable " + executable.path;

        var data = { engineName: executable.databaseName, file: executable.path, startedBy: "unknown@unknown.com", arguments: executable.arguments };

        $scope._StartExecutable(executable.databaseName, data, function (response) {
            // Success
            if (successCallback != null) {
                successCallback();
            }

        }, function (response) {
            // Error Starting Executable

            $scope._handleErrorReponse(response);

            if (errorCallback != null) {
                errorCallback(response);
            }

        });
    }


    $scope._AssureEngineIsRunning = function (database, job, successCallback, errorCallback) {

        $scope._GetEngine(database, function (response) {

            if (successCallback != null) {
                successCallback(response);
            }

            // Success
        }, function (response) {
            // Error

            if (response.status == 404) {
                // 404 Not Found (Engine not running)
                if (response.data.ServerCode == 10002) { // SCERRDATABASENOTFOUND 
                    // TODO: Create database
                    $scope.alerts.push({ type: 'info', msg: "Auto create databas is not implemented" });
                }

                var engineData = { Name: database.name, NoDb: false, LogSteps: false };    // TODO: get NoDb and LogSteps from arguments

                job.message = "Starting engine " + database.name;

                // Start Engine
                $scope._StartEngine(engineData, function () {
                    // Success
                    if (successCallback != null) {
                        successCallback();
                    }

                }, function (response) {
                    // Error Getting Engine
                    if (errorCallback != null) {
                        errorCallback(response);
                    }

                });

            }
            else {

                if (errorCallback != null) {
                    errorCallback(response);
                }

            }



        });

    }

    $scope.GetDatabase = function (name) {
        // Success
        for (var i = 0; i < $scope.databases.length; i++) {

            if ($scope.databases[i].name == name) {
                database = $scope.databases[i];
                return database;
            }
        }

        if (errorCallback != null) {
            errorCallback();
        }
        return null;
    }

    // Start database
    $scope._startDatabase = function (database, successCallback, errorCallback) {

        $http.post('/api/engines', { Name: database.name }).then(function (response) {
            // success

            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }
            });

        }, function (response) {
            // error

            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }
            });


        });

    }

    // Stop database
    $scope._stopDatabase = function (database, successCallback, errorCallback) {

        $http.delete(database.engineUri, { Name: name }).then(function (response) {
            // success handler
            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && successCallback != null) {
                    successCallback(response);
                }
            });

        }, function (response) {
            // error handler
            var count = 2;

            $scope._RefreshDatabases(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }

            });

            $scope._RefreshExecutables(function () {

                count--;

                if (count == 0 && errorCallback != null) {
                    errorCallback(response);
                }
            });


        });

    }

    // Verify database properties
    $scope._VerifyDatabaseProperties = function (properties, successCallback, errorCallback) {

        $http.post('/api/admin/verify/databaseproperties', properties).then(function (response) {
            // success handler

            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("validationErrors") == true) {

                if (successCallback != null) {
                    successCallback(response.data.validationErrors);
                }

            }
            else {
                $scope.showClientError("Unknown server response", null, "._VerifyDatabaseProperties() " + JSON.stringify(response));
                if (errorCallback != null) {
                    errorCallback();
                }
            }

        }, function (response) {
            // error handler
            $scope._handleErrorReponse(response);

            if (errorCallback != null) {
                errorCallback();
            }

        });

    }

    // Save database information
    $scope._SaveDatabaseSettings = function (database, successCallback, errorCallback) {

        $http.put('/api/admin/databases/' + database.name + '/settings', database).then(function (response) {
            // success handler

            if (response.data.hasOwnProperty("message") == true) {
                $scope.alerts.push({ type: 'success', msg: response.data.message });
            }

            if (response.data.hasOwnProperty("settings") == true) {
                if (successCallback != null) {
                    successCallback(response.data.settings);
                }
            }
            else {
                $scope.showClientError("Unknown server response", null, "._SaveDatabaseSettings() " + JSON.stringify(response.data));
                if (errorCallback != null) {
                    errorCallback();
                }
            }

        }, function (response) {
            // error handler
            $scope._handleErrorReponse(response);
            if (errorCallback != null) {
                errorCallback();
            }
        });



    }

    // Gets an database with status
    $scope._GetDefaultDatabaseSettings = function (successCallback, errorCallback) {

        $http.get('/api/admin/settings/database').then(function (response) {
            // success handler

            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("settings") == true) {

                if (successCallback != null) {
                    successCallback(response.data.settings);
                }
            }
            else {
                $scope.showClientError("Unknown server response", null, "._GetDefaultDatabaseSettings() " + JSON.stringify(response.data));
            }

        }, function (response) {
            // error handler
            $scope._handleErrorReponse(response);
            if (errorCallback != null) {
                errorCallback(null);
            }

        });

    }

    // Gets an database with status
    $scope._GetDatabaseSettings = function (name, successCallback, errorCallback) {

        $http.get('/api/admin/databases/' + name + '/settings').then(function (response) {
            // success handler

            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("settings") == true) {

                if (successCallback != null) {
                    successCallback(response.data.settings);
                }
            }
            else {
                $scope.showClientError("Unknown server response", null, "._GetDatabaseSettings() " + JSON.stringify(response.data));
            }

        }, function (response) {
            // error handler
            $scope._handleErrorReponse(response);
            if (errorCallback != null) {
                errorCallback(null);
            }

        });

    }

    // Verify database properties
    $scope._CreateDatabase = function (settings, successCallback, errorCallback) {

        $http.post('/api/admin/databases/' + settings.name + '/createdatabase', settings).then(function (response) {
            // success handler

            if (response.data.hasOwnProperty("errors") == true) {
                for (var i = 0; i < response.data.errors.length; i++) {
                    $scope.alerts.push({ type: 'error', msg: response.data.errors[i].message, helpLink: response.data.errors[i].helplink });
                }
                if (errorCallback != null) {
                    errorCallback();
                }

            }
            else {
                if (successCallback != null) {
                    successCallback();
                }

            }

        }, function (response) {
            // error handler

            if (response.status == 422) {
                // 422 Unprocessable Entity (WebDAV; RFC 4918)
                // The request was well-formed but was unable to be followed due to semantic errors
                if (response.data.hasOwnProperty("Text") == true) {
                    $scope.alerts.push({ type: 'error', msg: response.data.Text, helpLink: response.data.HelpLink });
                }
                else {
                    $scope._handleErrorReponse(response);
                }

            }
            else {
                $scope._handleErrorReponse(response);
            }

            if (errorCallback != null) {
                errorCallback();
            }

        });

    }

    // Get console output for one database
    $scope._GetConsole = function (database, successCallback, errorCallback) {


        // OLDWAY TO GET THE CONSOLE
        //$http.get('/api/admin/databases/' + database.name + '/console').then(function (response) {
        $http.get('/__' + database.name + '/console').then(function (response) {
            // success handler

            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("console") == true) {

                if (successCallback != null) {
                    successCallback(response.data.console);
                }

            }
            else {
                $scope.showClientError("Unknown server response", null, "._GetConsole() " + JSON.stringify(response));
                if (errorCallback != null) {
                    errorCallback();
                }
            }

        }, function (response) {
            // error handler

            if (response.status == 404) {
                // 404	Not Found
                var message = "Could not retrive the console output from the " + database.name + " database, Caused by a missing/not started database or there is no Executable running in the database";
                $scope.alerts.push({ type: 'error', msg: message });
            }
            else {
                $scope._handleErrorReponse(response);
            }

            if (errorCallback != null) {
                errorCallback();
            }

        });

    }

    // Check Latest version
    $scope._CheckLatestVersionInfo = function () {

        $http.get('/api/admin/versioncheck').then(function (response) {
            // success handler
            var versionCheck = response.data.VersionCheck;

            var currentDate = new Date(versionCheck.currentVersionDate);
            var currentDate_utc = new Date(currentDate.getUTCFullYear(), currentDate.getUTCMonth(), currentDate.getUTCDate(), currentDate.getUTCHours(), currentDate.getUTCMinutes(), currentDate.getUTCSeconds());

            var latestDate = new Date(versionCheck.latestVersionDate);
            var latestDate_utc = new Date(latestDate.getUTCFullYear(), latestDate.getUTCMonth(), latestDate.getUTCDate(), latestDate.getUTCHours(), latestDate.getUTCMinutes(), latestDate.getUTCSeconds());

            var days = Math.floor((latestDate_utc.getTime() - currentDate_utc.getTime()) / (1000 * 3600 * 24));

            // If version is more then 7 days old, notify the user
            if (days >= 7) {
                $scope.newVersion = {
                    days: days,
                    version: versionCheck.latestVersion,
                    downloadUri: versionCheck.latestVersionDownloadUri
                };
            }


        }, function (response) {
            // error handler

            if (response.status == 503) {
                // ServiceUnavailable
            }
            else {
                $scope._handleErrorReponse(response);
            }

        });
    }

    $scope._VersionCheck = function () {

        if (typeof (Storage) !== "undefined") {

            if (typeof (localStorage.lastVersionCheckUtcDate) !== "undefined") {

                var now = new Date();
                var now_utc = new Date(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), now.getUTCHours(), now.getUTCMinutes(), now.getUTCSeconds());

                var lastCheck = new Date(localStorage.lastVersionCheckUtcDate);
                var lastCheck_utc = new Date(lastCheck.getUTCFullYear(), lastCheck.getUTCMonth(), lastCheck.getUTCDate(), lastCheck.getUTCHours(), lastCheck.getUTCMinutes(), lastCheck.getUTCSeconds());

                var days = Math.floor((now_utc.getTime() - lastCheck_utc.getTime()) / (1000 * 3600 * 24));

                // Next check is 7 days after last check (until the user closes the notice
                if (days >= 7) {
                    $scope._CheckLatestVersionInfo();
                }
            }
            else {
                $scope._CheckLatestVersionInfo();
            }

        }
        else {
            // No web storage support..
            console.log("VersionCheck is disabled due to no web storage support..");
        }
    }
   
    $scope._VersionCheck();

}]);


/**
 * Executables Controller
 */
adminModule.controller('ExecutablesCtrl', ['$scope', '$routeParams', '$dialog', '$http', function ($scope, $routeParams, $dialog, $http) {

    $scope.alerts.length = 0;

    $scope.stopAllExecutables = function (database) {

        var job = $scope.addJob({ message: "Stopping all executables running in database " + database.name });
        $scope._StopAllExecutables(database, function () {

            $scope._RefreshExecutables(function () {
                // Done
                $scope.isBusy = false;

                // Remove job
                $scope.removeJob(job);

                // Refresh database statuses
                $scope._RefreshDatabases();

            });
        });
    }

    $scope.StopExecutable = function (executable) {

        var job = $scope.addJob({ message: "Stopping executable " + executable.path });

        $scope._StopExecutable(executable, function () {
            // Success
            $scope.removeJob(job);

            $scope._RefreshExecutables();
            $scope._RefreshDatabases();

            $scope.alerts.push({ type: 'success', msg: executable.path + " was stopped." });

        }, function () {
            // Error
            $scope.removeJob(job);
        });

    }

    $scope.RestartExecutable = function (executable) {

        var job = $scope.addJob({ message: "Restarting Executable" });

        $scope._ExecuteExecutable(executable, true, job, function (message) {
            // success

            // Remove job
            $scope.removeJob(job);

            $scope.alerts.push({ type: 'success', msg: executable.path + " was restarted." });


        }, function (response) {
            // Remove job
            $scope.removeJob(job);

            $scope._handleErrorReponse(response);

        });

    }

    $scope.btnStopAllExecutable = function (database) {

        $scope.alerts.length = 0;

        var title = 'Stop all running executable';
        var msg = 'Do you want to stop all executable running in database ' + database.name;
        var btns = [{ result: 0, label: 'Stop', cssClass: 'btn-danger' }, { result: 1, label: 'Cancel', cssClass: 'btn' }];

        $dialog.messageBox(title, msg, btns)
          .open()
          .then(function (result) {
              if (result == 0) {
                  $scope.stopAllExecutables(database);
              }
          });
    }

    $scope.btnRestartExecutable = function (executable) {

        $scope.alerts.length = 0;

        var title = 'Restart executable';
        var msg = 'Do you want to restart the executable ' + executable.path;
        var btns = [{ result: 0, label: 'Restart', cssClass: 'btn-danger' }, { result: 1, label: 'Cancel', cssClass: 'btn' }];

        $dialog.messageBox(title, msg, btns)
          .open()
          .then(function (result) {
              if (result == 0) {
                  $scope.RestartExecutable(executable);
              }
          });
    }

    $scope.btnStopExecutable = function (executable) {

        $scope.alerts.length = 0;

        var title = 'Stop executable';
        var msg = 'Do you want to stop the executable ' + executable.path;
        var btns = [{ result: 0, label: 'Stop', cssClass: 'btn-danger' }, { result: 1, label: 'Cancel', cssClass: 'btn' }];

        $dialog.messageBox(title, msg, btns)
          .open()
          .then(function (result) {
              if (result == 0) {
                  $scope.StopExecutable(executable);
              }
          });

    }

    // Init
    $scope._RefreshExecutables(function () {
        // Ready
    });

    $scope._RefreshDatabases(function () {
        // Ready
    });

    // AngularJS Buggworkaround
    // See this issue: https://github.com/angular/angular.js/issues/2797
    //$scope.isFalsey = function (val) {
    //    return !val;
    //}



}]);


/**
 * Executable Start Controller
 */
adminModule.controller('ExecutableStartCtrl', ['$scope', '$routeParams', '$location', function ($scope, $routeParams, $location) {

    $scope.alerts.length = 0;
    $scope.selectedDatabaseName = null;

    $scope.file = "";
    $scope.recentExecutables = [];

    $scope.StartExecutable = function (executable) {

        var job = $scope.addJob({ message: "Starting Executable" });

        var executable = {
            path: $scope.file,
            databaseName: $scope.selectedDatabaseName,
            arguments: []
        };

        $scope._ExecuteExecutable(executable, false, job, function (message) {
            // success

            // Remove job
            $scope.removeJob(job);

            // Remember successfully started executables
            $scope.rememberRecentFile($scope.file);

            $scope.alerts.push({ type: 'success', msg: executable.path + " was started." });


        }, function (response) {
            // Remove job
            $scope.removeJob(job);

        });

    }


    $scope.btn_startExecutable = function () {

        // Clear any previous alerts
        $scope.alerts.length = 0;

        $scope.StartExecutable();

    }

    $scope.btn_SetCurrent = function (file) {
        $scope.file = file.name;
    }

    $scope.rememberRecentFile = function (filename) {

        var maxItems = 5;
        // Check if file is already 'rememberd'
        for (var i = 0; i < $scope.recentExecutables.length ; i++) {

            // File already rememberd
            if (filename == $scope.recentExecutables[i].name) {
                return;
            }

        }
        $scope.recentExecutables.unshift({ name: filename });

        var toMany = $scope.recentExecutables.length - maxItems;

        if (toMany > 0) {
            $scope.recentExecutables.splice(maxItems, toMany);
        }

        localStorage.recentExecutables = JSON.stringify($scope.recentExecutables);
    }

    $scope.getRecentExecutables = function () {
        if (typeof (Storage) !== "undefined") {
            if (localStorage.recentExecutables != null) {
                $scope.recentExecutables = JSON.parse(localStorage.recentExecutables);
            }
        }
        else {
            // No web storage support..
        }
    }

    // Init
    $scope._RefreshExecutables(function () {
        // Ready
    });

    $scope._RefreshDatabases(function () {
        // Ready

        if ($scope.databases.length > 0) {
            $scope.selectedDatabaseName = $scope.databases[0].name;
        }

    });


    // Init
    $scope.getRecentExecutables();

    //$scope._RefreshDatabases(function () {
    //    if ($scope.databases.length > 0) {
    //        $scope.selectedDatabaseName = $scope.databases[0].name;
    //    }
    //});

}]);


/**
 * Databases Controller
 */
adminModule.controller('DatabasesCtrl', ['$scope', '$dialog', '$http', function ($scope, $dialog, $http) {

    //    $scope.alerts.length = 0;

    $scope.startDatabase = function (database) {

        var job = $scope.addJob({ message: "Starting database " + database.name });

        $scope._startDatabase(database, function () {
            // Success
            $scope.removeJob(job);
            $scope.alerts.push({ type: 'success', msg: "Database " + database.name + " was started" });
        }, function (response) {
            // Error
            $scope.removeJob(job);

            if (response.status == 409) {
                // 409 Conflict (Already running)
                if (response.data.hasOwnProperty("Text") == true) {
                    $scope.alerts.push({ type: 'error', msg: response.data.Text, helpLink: response.data.HelpLink });
                }
                else {
                    $scope._handleErrorReponse(response);
                }
            }
            else if (response.status == 422) {
                // 422 The request was well-formed but was unable to be followed due to semantic errors
                if (response.data.hasOwnProperty("Text") == true) {
                    $scope.alerts.push({ type: 'error', msg: response.data.Text, helpLink: response.data.HelpLink });
                }
                else {
                    $scope._handleErrorReponse(response);
                }
            }
            else {
                $scope._handleErrorReponse(response);
            }

        });

    }

    $scope.stopDatabase = function (database) {

        var job = $scope.addJob({ message: "Stopping database " + database.name });

        $scope._stopDatabase(database, function () {
            // Success
            $scope.removeJob(job);
            $scope.alerts.push({ type: 'success', msg: "Database " + database.name + " was stopped" });
        }, function () {
            // Error
            $scope.removeJob(job);

            if (response.status == 409) {
                // 409 Conflict (already stopped)
                if (response.data.hasOwnProperty("Text") == true) {
                    $scope.alerts.push({ type: 'error', msg: response.data.Text, helpLink: response.data.HelpLink });
                }
                else {
                    $scope._handleErrorReponse(response);
                }
            }
            else if (response.status == 422) {
                // 422 The request was well-formed but was unable to be followed due to semantic errors
                if (response.data.hasOwnProperty("Text") == true) {
                    $scope.alerts.push({ type: 'error', msg: response.data.Text, helpLink: response.data.HelpLink });
                }
                else {
                    $scope._handleErrorReponse(response);
                }
            }
            else {
                $scope._handleErrorReponse(response);
            }

        });

    }

    $scope.btnDeleteDatabase = function (database) {

        $scope.alerts.length = 0;

        var title = 'Stop engine';
        var msg = 'Do you want to delete the database ' + database.name;
        var btns = [{ result: 0, label: 'Delete', cssClass: 'btn-danger' }, { result: 1, label: 'Cancel', cssClass: 'btn' }];


        $dialog.messageBox(title, msg, btns)
          .open()
          .then(function (result) {
              if (result == 0) {
                  $scope.deleteDatabase(database);
              }
          });


    }

    $scope.btnStopDatabase = function (database) {

        $scope.alerts.length = 0;

        var title = 'Stop database';
        var msg = 'Do you want to stop the database ' + database.name;
        var btns = [{ result: 0, label: 'Stop', cssClass: 'btn-danger' }, { result: 1, label: 'Cancel', cssClass: 'btn' }];

        $dialog.messageBox(title, msg, btns)
          .open()
          .then(function (result) {
              if (result == 0) {
                  $scope.stopDatabase(database);
              }
          });


    }

    $scope.btnStartDatabase = function (database) {

        $scope.alerts.length = 0;

        var title = 'Start database';
        var msg = 'Do you want to start the database ' + database.name;
        var btns = [{ result: 0, label: 'Start', cssClass: 'btn-success' }, { result: 1, label: 'Cancel', cssClass: 'btn' }];

        $dialog.messageBox(title, msg, btns)
          .open()
          .then(function (result) {
              if (result == 0) {
                  $scope.startDatabase(database);
              }
          });


    }

    // Init
    $scope._RefreshDatabases();

}]);


/**
 * Database Controller
 */
adminModule.controller('DatabaseCtrl', ['$scope', '$routeParams', function ($scope, $routeParams) {

    $scope.alerts.length = 0;
    $scope.isExecutableRunning = false;
    $scope.console = "";
    $scope.isWebsocketSupport = ("WebSocket" in window);

    $scope.socket = null;

    $scope.$on('$destroy', function iVeBeenDismissed() {

        if ($scope.socket != null) {
            if ($scope.socket.readyState == 0 || $scope.socket.readyState == 2 || $scope.socket.readyState == 3) return; // (0) CONNECTING // (2) CLOSING, (3) CLOSED
            $scope.socket.close();
        }

    })

    // Websockets
    // Retrive the console output for a specific database
    $scope.listenToConsoleOutputs = function () {

        try {
            // TODO
            $scope.socket = new WebSocket("ws://" + location.host + "/__" + $scope.database.name + "/console/ws");

            this.socket.onopen = function (evt) {
                $scope.socket.send("PING");
            };

            this.socket.onmessage = function (evt) {

                if (evt.data == null) {
                    $scope.console = "";;
                }
                else {
                    $scope.console += evt.data.replace(/\r\n/g, "<br>");
                }

                if ($scope.console.length > 8000) {
                    $scope.console = $scope.console.substr($scope.console.length - 8000);
                }


                $scope.$apply();
                $("#console").scrollTop($("#console")[0].scrollHeight); // TODO: Do this in the next cycle?
            };
            this.socket.onerror = function (evt) {
                console.log("Console websockets onerror:" + evt);
                $scope.isWebsocketSupport = false;
                $scope.$apply();
            };
        }
        catch (exception) {
            console.log("Console websockets exception:" + exception);
            $scope.isWebsocketSupport = false;
        }
    }

    // Standard
    // Retrive the console output for a specific database
    $scope.getConsole = function (database) {

        $scope._GetConsole(database, function (text) {
            // Success

            if (text == null) {
                $scope.console = "";;
            }
            else {
                $scope.console = text.replace(/\r\n/g, "<br>");
            }

            $("#console").scrollTop($("#console")[0].scrollHeight); // TODO: Do this in the next cycle?

        }, function () {
            // Error
        });

    }
    // User clicked the "Refresh" button
    $scope.btnClick_refreshConsole = function () {
        $scope.alerts.length = 0;
        $scope.getConsole($scope.database);
    }

    // Init
    $scope.isBusy = true;
    $scope._RefreshDatabases(function () {
        // Ready
        for (var i = 0 ; i < $scope.databases.length ; i++) {
            if ($scope.databases[i].name == $routeParams.name) {
                $scope.database = $scope.databases[i];
                break;

            }
        }
        $scope.isBusy = false;
        if ($scope.isWebsocketSupport) {
            $scope.listenToConsoleOutputs();
        }

    });

    // Console fixe the height.
    var $window = $(window);
    $scope.winHeight = $window.height();
    $scope.winWidth = $window.width();
    $window.resize(function () {
        $scope.winHeight = $window.height();
        $scope.winWidth = $window.width();
        $scope.$apply();
    });

    $scope.style = function () {
        return {
            'height': ($scope.calcHeight()) + 'px',
            'width': + '100%'
        };
    }

    $scope.calcHeight = function () {
        var border = 12;
        //var topOffset = $("#console").offset().top;
        var ht = $("#console");
        var offset = ht.offset();
        if (!offset) {
            return;
        }
        var topOffset = offset.top;

        var height = $scope.winHeight - topOffset - 2 * border;
        if (height < 150) {
            return 150;
        }
        return height;
    };

    $scope.calcWidth = function () {
        var border = 12;
        var leftOffset = $("#console").offset().left;
        var width = $scope.winWidth - leftOffset - 2 * border;
        if (width < 150) {
            return 150;
        }
        return width;
    };

}]);


/**
 * Database Edit Controller
 */
adminModule.controller('DatabaseEditCtrl', ['$scope', '$routeParams', '$http', function ($scope, $routeParams, $http) {

    $scope.alerts.length = 0;
    $scope.settings = null;

    // Refresh database settings
    $scope.refreshSettings = function () {

        $scope._GetDatabaseSettings($routeParams.name, function (settings) {
            // Success
            $scope.settings = settings;
            $scope.myForm.$setPristine(); // This disent work, the <select> breaks the pristine state :-(

        }, function () {
            // Error
            $scope.settings = null;
        });

    }

    // Save database settings
    $scope.saveSettings = function (name) {
        $scope.isBusy = true;

        $http({ method: 'PUT', url: ' /api/admin/databases/databases/' + $scope.database.name, data: $scope.database }).

          // A response status code between 200 and 299 is considered a success status
          success(function (response, status, headers, config) {
              $scope.isBusy = false;

              if (status == 200) {
                  // 200 Ok
                  $scope.isBusy = true;

                  if (response.message != null) {
                      $scope.alerts.push({ type: 'success', msg: response.message });
                  }

                  $scope.database = response.database;

                  if (response.database == null) {
                      $scope.showClientError("Invalid response, database property was null", null, ".saveSettings()");
                      //                      $scope.showException("Invalid response, database property was null", null, ".saveSettings()");
                  }
                  else {
                      $scope.myForm.$setPristine();
                  }

              }
              else {
                  $scope.showServerError("Unhandled http statuscode " + status, null, ".saveSettings()");
              }

          }).
          error(function (response, status, headers, config) {
              $scope.isBusy = false;

              if (status == 403) {
                  // 403 Forbidden (Validation Error)
                  if (response.validationErrors != null && response.validationErrors.length > 0) {

                      for (var i = 0; i < response.validationErrors.length; i++) {
                          $scope.alerts.push({ type: 'error', msg: response.validationErrors[i].message });
                          $scope.myForm[response.validationErrors[i].property].$setValidity("validationError", false);
                          var id = response.validationErrors[i].property;
                          var unregister = $scope.$watch("settings." + response.validationErrors[i].property, function (newValue, oldValue) {
                              if (newValue == oldValue) return;
                              $scope.myForm[id].$setValidity("validationError", true);
                              unregister();
                          }, false);

                      }
                  }
                  else {
                      $scope.showServerError("The return code '403 Forbidden' did not return any validation error fields.", null, null);
                  }

              }
              else if (status == 500) {
                  // 500 Internal Server Error
                  $scope.showServerError(response.message, response.helpLink, response.stackTrace);
              }
              else {
                  $scope.showClientError("Unhandled http statuscode " + status, null, ".createDatabase()");
              }

          });
    }

    $scope.btn_SaveSettings = function () {
        $scope.alerts.length = 0;

        var job = $scope.addJob({ message: "Verifying properties" });

        $scope._VerifyDatabaseProperties($scope.settings, function (validationErrors) {
            // Validation done

            job.message = "Saving settings " + $scope.settings.name;

            if (validationErrors.length == 0) {
                // No validation errors, goahead creating database

                $scope._SaveDatabaseSettings($scope.settings, function () {
                    // Success
                    $scope.removeJob(job);
                    $scope.myForm.$setPristine();
                }, function () {
                    $scope.removeJob(job);
                    // Error
                });
            }
            else {

                $scope.removeJob(job);

                // Show errors on screen
                for (var i = 0; i < validationErrors.length; i++) {
                    $scope.alerts.push({ type: 'error', msg: validationErrors[i].message });
                    $scope.myForm[validationErrors[i].property].$setValidity("validationError", false);
                    var id = validationErrors[i].property;
                    var unregister = $scope.$watch("settings." + validationErrors[i].property, function (newValue, oldValue) {
                        if (newValue == oldValue) return;
                        $scope.myForm[id].$setValidity("validationError", true);
                        unregister();
                    }, false);

                }

            }


        }, function () {
            //
            $scope.removeJob(job);

        });


        //$scope.createDatabase();
    }

    $scope.btn_ResetSettings = function () {
        $scope.alerts.length = 0;
        $scope.refreshSettings();
    }

    // Init
    $scope.refreshSettings();


}]);


/**
 * Database Create Controller
 */
adminModule.controller('DatabaseCreateCtrl', ['$scope', '$http', '$location', '$anchorScroll', function ($scope, $http, $location, $anchorScroll) {

    $scope.isBusy = false;
    $scope.alerts.length = 0;

    // Refresh database settings
    $scope.refreshSettings = function () {

        $scope._GetDefaultDatabaseSettings(function (settings) {
            // Success

            settings.tempDirectory = settings.tempDirectory.replace("[DatabaseName]", settings.name);
            settings.imageDirectory = settings.imageDirectory.replace("[DatabaseName]", settings.name);
            settings.transactionLogDirectory = settings.transactionLogDirectory.replace("[DatabaseName]", settings.name);
            settings.dumpDirectory = settings.dumpDirectory.replace("[DatabaseName]", settings.name);

            $scope.settings = settings;
            $scope.myForm.$setPristine(); // This disent work, the <select> breaks the pristine state :-(

        }, function () {
            // Error
            $scope.settings = null;
        });

    }

    $scope.btn_CreateDatabase = function () {

        $scope.alerts.length = 0;
        var job = $scope.addJob({ message: "Verifying properties" });

        // Scroll top top
        $location.hash('top');
        $anchorScroll();


        $scope._VerifyDatabaseProperties($scope.settings, function (validationErrors) {
            // Validation done

            job.message = "Creating database " + $scope.settings.name;

            if (validationErrors.length == 0) {
                // No validation errors, goahead creating database

                $scope._CreateDatabase($scope.settings, function () {
                    // Success
                    $location.hash("");

                    $scope.removeJob(job);
                    $scope.alerts.push({ type: 'success', msg: "Database " + $scope.settings.name + " was created." });

                    $location.path("/databases");


                }, function () {
                    $scope.removeJob(job);
                    // Error
                });
            }
            else {

                $scope.removeJob(job);

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


        }, function () {
            //
            $scope.removeJob(job);

        });


        //$scope.createDatabase();
    }

    $scope.btn_ResetSettings = function () {

        $scope.alerts.length = 0;
        $scope.refreshSettings();

    }

    // init
    $scope.refreshSettings();



}]);


/**
 * Server Edit Controller
 */
adminModule.controller('ServerEditCtrl', ['$scope', '$http', function ($scope, $http) {

    $scope.alerts.length = 0;
    $scope.settings = null;

    $scope.refreshSettings = function () {

        $scope._GetServerSettings("personal", function (settings) {
            // Success
            $scope.settings = settings;
            $scope.myForm.$setPristine(); // This disent work, the <select> breaks the pristine state :-(

        }, function () {
            // Error
            $scope.settings = null;
        });

    }

    $scope.btn_SaveSettings = function () {

        $scope.alerts.length = 0;

        var job = $scope.addJob({ message: "Verifying properties" });

        $scope._VerifyServerProperties($scope.settings, function (validationErrors) {
            // Validation done

            job.message = "Saving settings " + $scope.settings.name;

            if (validationErrors.length == 0) {
                // No validation errors, goahead creating database

                $scope._SaveServerSettings($scope.settings, function () {
                    // Success
                    $scope.removeJob(job);
                    $scope.myForm.$setPristine();
                }, function () {
                    // Error
                    $scope.removeJob(job);
                });


            }
            else {

                $scope.removeJob(job);

                // Show errors on screen
                for (var i = 0; i < validationErrors.length; i++) {
                    $scope.alerts.push({ type: 'error', msg: validationErrors[i].message });
                    $scope.myForm[validationErrors[i].property].$setValidity("validationError", false);
                    var id = validationErrors[i].property;
                    var unregister = $scope.$watch("settings." + validationErrors[i].property, function (newValue, oldValue) {
                        if (newValue == oldValue) return;
                        $scope.myForm[id].$setValidity("validationError", true);
                        unregister();
                    }, false);

                }


            }


        }, function () {
            //
            $scope.removeJob(job);

        });

    }

    $scope.btn_ResetSettings = function () {
        $scope.alerts.length = 0;
        $scope.refreshSettings();
    }

    // Init
    $scope.refreshSettings();
}]);


/**
 * Gateway Controller
 */
adminModule.controller('GatewayCtrl', ['$scope', '$http', function ($scope, $http) {

    $scope.alerts.length = 0;

    // Get Gateway information
    $scope.refreshGatewayStats = function () {

        $http.get('/gwstats').then(function (response) {
            // success handler
            $scope.gwStats = response.data;

        }, function (response) {
            // error handler
            $scope.gwStats = "";
            $scope._handleErrorReponse(response);
        });

    }

    // User clicked the "Refresh" button
    $scope.btnRefresh = function () {
        $scope.alerts.length = 0;
        $scope.refreshGatewayStats();
    }

    // Init
    $scope.refreshGatewayStats();

}]);


/**
 * Log Controller
 */
adminModule.controller('LogCtrl', ['$scope', '$http', '$location', function ($scope, $http, $location) {

    $scope.alerts.length = 0;
    $scope.log = {};
    $scope.log.LogEntries = [];
    $scope.isWebsocketSupport = ("WebSocket" in window);

    $scope.filterModel = {
        debug: false,
        notice: false,
        warning: true,
        error: true,
        source: ""
    };

    $scope.list_of_string = []

    $scope.select2Options = {
        'multiple': true,
        'simple_tags': true,
        'tags': []
    };

    $scope.socket = null;

    $scope.$on('$destroy', function iVeBeenDismissed() {

        if ($scope.socket != null) {
            if ($scope.socket.readyState == 0 || $scope.socket.readyState == 2 || $scope.socket.readyState == 3) return; // (0) CONNECTING // (2) CLOSING, (3) CLOSED
            $scope.socket.close();
        }
    })

    // Set the filters from the address bar parameters to the controller
    $scope.filterModel = $location.search();


    // Watch for changes in the filer
    $scope.$watch('filterModel', function () {
        // Filter changed, update the address bar
        $location.search($scope.filterModel);
    }, true);

    //// Watch for changes in the filer
    $scope.$watch('list_of_string', function () {
        // Filter changed, update the address bar

        var sourceFilter = "";
        for (var i = 0 ; i < $scope.list_of_string.length; i++) {
            if (sourceFilter != "") sourceFilter += ";";

            // Special handling, sometime the list_of_string can be objects and some othertimes it's string. no clue why!?!
            if ($scope.list_of_string[i].hasOwnProperty('id')) {
                sourceFilter += $scope.list_of_string[i].id;
            }
            else {
                sourceFilter += $scope.list_of_string[i];
            }
        }

        if ($scope.filterModel.source != sourceFilter) {
            $scope.filterModel.source = sourceFilter;
        }


    }, true);


    var arr = $scope.filterModel.source.split(';');
    for (var i = 0 ; i < arr.length; i++) {
        if (arr[i] == "") continue;
        $scope.list_of_string.push(arr[i]);
    }

    // Retrive log information
    $scope.getLog = function () {

        $scope.select2Options.tags.length = 0;

        $http.get('/api/admin/log', { params: $scope.filterModel }).then(function (response) {
            // success handler
            $scope.log = response.data;

            var filterSourceOptions = $scope.log.FilterSource.split(";");
            for (var i = 0; i < filterSourceOptions.length ; i++) {
                $scope.select2Options.tags.push(filterSourceOptions[i]);
            }

        }, function (response) {
            // error handler
            $scope.log = "";
            $scope._handleErrorReponse(response);
        });

    }

    // Websockets
    // Retrive the event when the log has changed
    $scope.listenToLogEvents = function () {

        try {
            $scope.socket = new WebSocket("ws://" + location.host + "/api/admin/log/event/ws");

            this.socket.onopen = function (evt) {
                $scope.socket.send("PING");
            };

            this.socket.onclose = function (evt) {
                $scope.socket = null;
            };

            this.socket.onmessage = function (evt) {

                if (evt.data == "1") {
                    // 1 = Log has change
                    $scope.$apply();
                    $scope.getLog();
                }
            };

            this.socket.onerror = function (evt) {
                console.log("Log websocket onerror:" + evt);
                $scope.isWebsocketSupport = false;
                $scope.$apply();
                $scope.getLog();
            };
        }
        catch (exception) {
            console.log("Log websocket exception:" + exception);
            $scope.isWebsocketSupport = false;
            $scope.getLog();
        }
    }

    $scope.btnRefresh = function () {
        $scope.alerts.length = 0;
        $scope.getLog();
    }

    if ($scope.isWebsocketSupport) {
        $scope.listenToLogEvents();
    }

    // Init 
    $scope.getLog();

  
}]);


/**
 * Dialog Controller
 */
adminModule.controller('DialogCtrl', ['$scope', 'dialog', function ($scope, dialog) {

    $scope.header = dialog.options.data.header;
    $scope.message = dialog.options.data.message;
    $scope.helpLink = dialog.options.data.helpLink;
    $scope.stackTrace = dialog.options.data.stackTrace;

    $scope.close = function (result) {
        dialog.close(result);
    };

}]);

/**
 * Retrives the relative path of an url
 * Example:
 * Input: http://localhost:8080/foo/bar?123
 * Output: /foo/bar
 */
function toRelativePath(url) {
    var a = document.createElement('a');
    a.href = url;
    return a.pathname;
}
