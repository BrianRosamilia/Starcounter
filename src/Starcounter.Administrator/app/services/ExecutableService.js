/**
 * ----------------------------------------------------------------------------
 * Executables Service
 * ----------------------------------------------------------------------------
 */
adminModule.service('ExecutableService', ['$http', '$log', 'UtilsFactory', 'JobFactory', function ($http, $log, UtilsFactory, JobFactory) {

    // List of executables
    //  {
    //      "path":"c:\path\to\executable\foo.exe",
    //      "uri":"http://example.com/foo.exe-12345",
    //      "databaseName":"default",
    //      "applicationFilePath":""
    //  }
    this.executables = [];

    var self = this;


    /**
     * Get all running executables
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.getExecutables = function (successCallback, errorCallback) {

        $log.info("Retriving executables");

        var errorHeader = "Failed to retrive a list of executables";
        var uri = "/api/admin/executables";


        // Example JSON response 
        //-----------------------
        //{
        //  "Executables":[
        //      {
        //          "path":"c:\path\to\executable\foo.exe",
        //          "uri":"http://example.com/foo.exe-12345",
        //          "databaseName":"default",
        //          "applicationFilePath":""
        //      }
        //  ]
        //}
        $http.get(uri).then(function (response) {
            // Success

            // Validate response
            if (response.data.hasOwnProperty("Executables") == true) {
                $log.info("Executables (" + response.data.Executables.length + ") successfully retrived");
                if (typeof (successCallback) == "function") {
                    successCallback(response.data.Executables);
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
     * Refresh executables
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.refreshExecutables = function (successCallback, errorCallback) {

        this.getExecutables(function (executables) {
            // Success

            // TODO: Update current executable with new values
            //       instead of replacing the executables list

            // Clear executable list
            self.executables.length = 0;

            // Populate the executable list with the response
            for (var i = 0; i < executables.length; i++) {
                self.executables.push(executables[i]);
            }

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
     * Restart Executable
     * @param {Executable} executable
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.restartExecutable = function (executable, successCallback, errorCallback) {

        this.stopExecutable(executable, function () {
            // Success
            self.startExecutable(executable.path, executable.databaseName, successCallback, errorCallback)

        }, function (messageObject) {
            // Error
            if (typeof (errorCallback) == "function") {
                errorCallback(messageObject);
            }
        })
    }


    /**
     * Start Executable
     * @param {Executable} executable
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.startExecutable = function (file, databaseName, successCallback, errorCallback) {

        $log.info("Starting executable");

        this.startEngine(databaseName, function () {
            // Success
            var bodyData = { Path: file, StartedBy: "startedBy" };

            // Add job
            var job = { message: "Starting executable " + file + " in " + databaseName };
            JobFactory.AddJob(job);

            // Example JSON response 
            //-----------------------
            // 201
            // {
            //     "Uri": "http://example.com/api/executables/foo/foo.exe-123456789",
            //     "Path": "C:\\path\to\\the\\exe\\foo.exe",
            //     "ApplicationFilePath" : "C:\\optional\\path\to\\the\\input\\file.cs",
            //     "Description": "Implements the Foo module",
            //     "Arguments": [{"dummy":""}],
            //     "DefaultUserPort": 1,
            //     "ResourceDirectories": [{"dummy":""}],
            //     "WorkingDirectory": "C:\\path\\to\\default\\resource\\directory",
            //     "IsTool":false,
            //     "StartedBy": "Per Samuelsson, per@starcounter.com",
            //     "Engine": {
            //         "Uri": "http://example.com/api/executables/foo"
            //     },
            //     "RuntimeInfo": {
            //         "LoadPath": "\\relative\\path\\to\\weaved\\server\\foo.exe",
            //         "Started": "ISO-8601, e.g. 2013-04-25T06.24:32",
            //         "LastRestart": "ISO-8601, e.g. 2013-04-25T06.49:01"
            //     }
            // }
            $http.post('/api/engines/' + databaseName + '/executables', bodyData).then(function (response) {
                // Success
                JobFactory.RemoveJob(job);

                $log.info("Executable " + response.data.Path + " was successfully started");

                // Refresh executables
                self.refreshExecutables(successCallback, errorCallback);

                // TODO: Return the started executable

            }, function (response) {
                // Error
                JobFactory.RemoveJob(job);

                var errorHeader = "Failed to start executable";

                var messageObject;
                if (response instanceof SyntaxError) {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.message, null, response.stack);
                }
                else if (response.status == 404) {
                    // 404 A database with the specified name was not found.
                    messageObject = UtilsFactory.createMessage(errorHeader, response.data.Text, response.data.Helplink);
                }
                else if (response.status == 409) {
                    // 409 The executable is already running or the Engine is not started.
                    messageObject = UtilsFactory.createMessage(errorHeader, response.data.Text, response.data.Helplink);
                }
                else if (response.status == 422) {
                    // 422 The executable can not be found or The weaver failed to load a binary user code file.
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
                    // Unhandle error
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.Text, response.data.Helplink, null);
                }

                $log.error(errorHeader, response);

                if (typeof (errorCallback) == "function") {
                    errorCallback(messageObject);
                }

            });

        }, function (errorMessageObject) {
            // Error
            if (typeof (errorCallback) == "function") {
                errorCallback(errorMessageObject);
            }
        });

    }


    /**
     * Stop Executable
     * @param {Executable} executable
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.stopExecutable = function (executable, successCallback, errorCallback) {

        $log.info("Stopping executable");

        var job = { message: "Stopping executable " + executable.path };
        JobFactory.AddJob(job);

        var uri = UtilsFactory.toRelativePath(executable.uri);

        $http.delete(uri).then(function (response) {
            // Success, 204 No Content 
            JobFactory.RemoveJob(job);

            $log.info("Executable " + executable.path + " was successfully stopped");

            // Refresh databases
            self.refreshExecutables(successCallback, errorCallback);

        }, function (response) {
            JobFactory.RemoveJob(job);

            // Error
            var errorHeader = "Failed to stop executable";
            var messageObject;

            if (response instanceof SyntaxError) {
                messageObject = UtilsFactory.createErrorMessage(errorHeader, response.message, null, response.stack);
            }
            else if (response.status == 404) {
                // 404 A database with the specified name was not found.
                messageObject = UtilsFactory.createMessage(errorHeader, response.data.Text, response.data.Helplink);
            }
            else if (response.status == 409) {
                // 409 The executable is already running or the Engine is not started.
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
     * Start engine/database
     * @param {name} name
     * @return {Promise} promise
     */
    this.startEngine = function (name, successCallback, errorCallback) {

        $log.info("Starting engine");

        var errorHeader = "Failed to start database";

        var job = { message: "Starting database " + name };
        JobFactory.AddJob(job);

        var engineData = { Name: name, NoDb: false, LogSteps: false };    // TODO: get NoDb and LogSteps from arguments
        var uri = "/api/engines";

        // Example JSON response (200 Ok)
        //-----------------------
        // {
        //     "CodeHostCommandLineAdditions" : "",
        //     "LogSteps" : false,
        //     "Name" : "default",
        //     "NoDb" : false,
        //     "Uri" : "http://headsutv19:8181/api/engines/tracker"
        // }
        //
        // Example JSON response (201 Created)
        //-----------------------
        // {
        // "Uri":"http://headsutv19:8181/api/engines/tracker",
        // "NoDb":false,
        // "LogSteps":false,
        // "Database":{
        //      "Name":"tracker",
        //      "Uri":"http://headsutv19:8181/api/databases/tracker"
        // },
        // "DatabaseProcess":{
        //      "Uri":"http://headsutv19:8181/api/engines/tracker/db",
        //      "Running":true
        // },
        // "CodeHostProcess":{
        //      "Uri":"http://headsutv19:8181/api/engines/tracker/host",
        //      "PID":11136
        // },
        // "Executables":{
        //      "Uri":"http://headsutv19:8181/api/engines/tracker/executables",
        //      "Executing":[]
        // }
        $http.post(uri, engineData).then(function (response) {
            // Success
            // 200 OK
            // 201 Created

            JobFactory.RemoveJob(job);

            $log.info("Engine " + name + " started successfully");

            if (typeof (successCallback) == "function") {
                successCallback();
            }


        }, function (response) {
            // Error
            JobFactory.RemoveJob(job);

            var errorHeader = "Failed to start executable";

            var messageObject;
            if (response instanceof SyntaxError) {
                messageObject = UtilsFactory.createErrorMessage(errorHeader, response.message, null, response.stack);
            }
            else if (response.status == 404) {
                // 404 A database with the specified name was not found.
                messageObject = UtilsFactory.createMessage(errorHeader, response.data.Text, response.data.Helplink);
            }
            else if (response.status == 422) {
                // 422 The executable can not be found or The weaver failed to load a binary user code file.
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
                // Unhandle error
                messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.Text, response.data.Helplink, null);
            }

            $log.error(errorHeader, response);

            if (typeof (errorCallback) == "function") {
                errorCallback(messageObject);
            }

        });

    }


}]);