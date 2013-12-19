/**
 * ----------------------------------------------------------------------------
 * Databases Service
 * ----------------------------------------------------------------------------
 */
adminModule.service('DatabaseService', ['$http', '$log', 'UtilsFactory', 'JobFactory', function ($http, $log, UtilsFactory, JobFactory) {

    // List of databases
    // {
    //     "Name":"tracker",
    //     "Uri":"http://machine:1234/api/databases/mydatabase",
    //     "HostUri":"http://machine:1234/api/engines/mydatabase/db",
    //     "Running":true
    // }
    this.databases = [];

    var self = this;


    /**
     * Get all databases
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.getDatabases = function (successCallback, errorCallback) {

        $log.info("Retriving databases");

        var errorHeader = "Failed to retrive a list of databases";
        var uri = "/api/admin/databases";

        $http.get(uri).then(function (response) {
            // Success

            // Validate response
            if (response.data.hasOwnProperty("Databases") == true) {
                $log.info("Databases (" + response.data.Databases.length + ") successfully retrived");
                if (typeof (successCallback) == "function") {
                    successCallback(response.data.Databases);
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


    /**
     * Get database
     * @param {databaseName} Database name
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.getDatabase = function (databaseName, successCallback, errorCallback) {


        for (var i = 0 ; i < self.databases.length ; i++) {
            if (self.databases[i].name == databaseName) {
                return self.databases[i];
            }
        }

    }


    /**
     * Refresh databases
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.refreshDatabases = function (successCallback, errorCallback) {

        this.getDatabases(function (databases) {
            // Success

            // TODO: Update current database with new values
            //       instead of replacing the database list

            // Clear database list
            self.databases.length = 0;

            // Populate the database list with the response
            for (var i = 0; i < databases.length; i++) {
                self.databases.push(databases[i]);
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
     * Start database
     * @param {Database} database
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.startDatabase = function (database, successCallback, errorCallback) {

        $log.info("Starting database " + database.name);

        var job = { message: "Starting database " + database.name };

        JobFactory.AddJob(job);

        $http.post('/api/engines', { Name: database.name }).then(function (response) {
            // Success
            // 200
            // 201
            JobFactory.RemoveJob(job);

            $log.info("Database " + database.name + " was successfully started");

            // Refresh databases
            self.refreshDatabases(successCallback, errorCallback);

        }, function (response) {
            JobFactory.RemoveJob(job);
            // Error
            var errorHeader = "Failed to start database";
            var messageObject;

            if (response instanceof SyntaxError) {
                messageObject = UtilsFactory.createErrorMessage(errorHeader, response.message, null, response.stack);
            }
            else if (response.status == 404) {
                // 404 A database with the specified name was not found.
                messageObject = UtilsFactory.createMessage(errorHeader, response.data.Text, response.data.Helplink);
            }
            else if (response.status == 422) {
                // 422 Database engine name not specified
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
     * Stop database
     * @param {Database} database
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.stopDatabase = function (database, successCallback, errorCallback) {

        $log.info("Stopping database " + database.name);

        var errorHeader = "Failed to stop database";
        var job = { message: "Stopping database " + database.name };

        JobFactory.AddJob(job);
        var databaseUri = UtilsFactory.toRelativePath(database.engineUri);

        $http.delete(databaseUri, { Name: name }).then(function (response) {
            // Success
            // 202
            // 204
            JobFactory.RemoveJob(job);

            $log.info("Database " + database.name + " was successfully stopped");

            // Refresh databases
            self.refreshDatabases(successCallback, errorCallback);

        }, function (response) {
            JobFactory.RemoveJob(job);
            // Error
            var errorHeader = "Failed to stop database";
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
     * Get Database settings
     * @param {Database} database
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.getDatabaseSettings = function (database, successCallback, errorCallback) {

        $log.info("Retriving database settings");

        var errorHeader = "Failed to retrive database settings";

        var job = { message: "Retriving database settings" };
        JobFactory.AddJob(job);
        var uri = "/api/admin/databases/" + database.name + "/settings";

        $http.get(uri).then(function (response) {
            // success handler
            JobFactory.RemoveJob(job);

            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("settings") == true) {
                $log.info("Databases settings successfully retrived");
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

            JobFactory.RemoveJob(job);
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
     * Save database settings
     * @param {database} Database
     * @param {settings} settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.saveDatabaseSettings = function (database, settings, successCallback, errorCallback) {

        $log.info("Saving database settings");

        var errorHeader = "Failed to save database settings";

        var job = { message: "Saving database settings" };
        JobFactory.AddJob(job);
        var uri = "/api/admin/databases/" + database.name + "/settings";

        $http.put(uri, settings).then(function (response) {
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
            else if (response.status == 500) {
                // 500 Server Error
                errorHeader = "Internal Server Error";
                if (response.data.hasOwnProperty("Text") == true) {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.Text, response.data.Helplink, null);
                }
                else if (response.data.hasOwnProperty("exception") == true) {
                    messageObject = UtilsFactory.createErrorMessage(errorHeader, response.data.exception.message, response.data.exception.helpLink, response.data.exception.stackTrace);
                }
                else {
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
     * Get Database default settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.getDatabaseDefaultSettings = function (successCallback, errorCallback) {

        $log.info("Retriving database defult settings");

        var errorHeader = "Failed to retrive database default settings";

        var job = { message: "Retriving database defult settings" };
        JobFactory.AddJob(job);

        $http.get('/api/admin/settings/database').then(function (response) {
            // success handler
            JobFactory.RemoveJob(job);

            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("settings") == true) {
                $log.info("Default Databases settings successfully retrived");
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

            JobFactory.RemoveJob(job);
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
     * Verify database settings
     * @param {settings} Settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.verifyDatabaseSettings = function (settings, successCallback, errorCallback) {

        $log.info("Verifying database settings");

        var errorHeader = "Failed to verify database settings";
        var job = { message: "Verifying database settings" };
        JobFactory.AddJob(job);

        $http.post('/api/admin/verify/databaseproperties', settings).then(function (response) {
            // success handler
            JobFactory.RemoveJob(job);

            if (response.hasOwnProperty("data") == true && response.data.hasOwnProperty("validationErrors") == true) {

                $log.info("Default Databases settings successfully verified");
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
     * Create database
     * @param {settings} settings
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.createDatabase = function (settings, successCallback, errorCallback) {

        $log.info("Creating database");

        var errorHeader = "Failed to create database";

        var job = { message: "Creating database " + settings.name };
        JobFactory.AddJob(job);

        $http.post('/api/admin/databases/' + settings.name + '/createdatabase', settings).then(function (response) {
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

                if (successCallback != null) {
                    // TODO: Return the newly create database
                    successCallback();
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
