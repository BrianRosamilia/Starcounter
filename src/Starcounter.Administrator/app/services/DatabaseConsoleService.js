/**
 * ----------------------------------------------------------------------------
 * Database Console output Service
 * TODO: Listeners per database
 * ----------------------------------------------------------------------------
 */
adminModule.service('DatabaseConsoleService', ['$http', '$log', '$rootScope', 'UtilsFactory', 'JobFactory', function ($http, $log, $rootScope, UtilsFactory, JobFactory) {

    this.isWebsocketSupported = ("WebSocket" in window)

    // Listeners
    // { databaseName:"default", onEvent: function() {}, onError : function() {} }
    this.listeners = [];

    // List of socket connections, one connection per database.
    // { socket:socket, databaseName:"default" }
    this.connections = [];

    var self = this;

    /**
     * Get log Entries
     * @param {databaseName} Database name
     * @param {successCallback} successCallback function
     * @param {errorCallback} errorCallback function
     */
    this.getConsoleOutput = function (databaseName, successCallback, errorCallback) {

        $log.info("Retriving console output for database " + databaseName);

        var errorHeader = "Failed to retrive the database console output";
        var uri = "/__" + databaseName + "/console";


        $http.get(uri).then(function (response) {
            // Success

            // Validate response
            if (response.data.hasOwnProperty("console") == true) {
                $log.info("Database console output successfully retrived");
                if (typeof (successCallback) == "function") {
                    successCallback(response.data.console);
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

            else if (response.status == 404) {
                // 404 Not found
                var message = "Could not retrive the console output from the " + databaseName + " database, Caused by a not started database or there is no Executable running in the database";
                messageObject = UtilsFactory.createMessage(errorHeader, message, response.data.Helplink);
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
     * Register log listener
     * @param {listener} { databaseName:"default", onEvent: function () { },  onError: function (messageObject) {}  }
     */
    this.registerEventListener = function (listener) {
        this.listeners.push(listener);

        var connection = this.getConnection(listener.databaseName);
        if (connection == null) {
            this.startListener(listener.databaseName);
        }

    }


    /**
     * Unregister log listener
     * @param {listener} { databaseName:"default", onEvent: function () { },  onError: function (messageObject) {}  }
     */
    this.unregisterEventListener = function (listener) {

        // Remove listener from list
        var index = this.listeners.indexOf(listener);
        if (index > -1) {
            this.listeners.splice(index, 1);
        }

        // Determine if there is no more connections to a database
        var bCloseConnection = false;
        for (var i = 0; i < this.connections.length ; i++) {
            if (this.connections[i].databaseName == listener.databaseName) {
                bCloseConnection = true;
                break;
            }
        }

        if (bCloseConnection) {
            this.stopListener(listener.databaseName);
        }

    }


    /**
     * Get database connection 
     * @param {databaseName} Database name
     */
    this.getConnection = function (databaseName) {

        for (var i = 0; i < this.connections.length ; i++) {
            if (this.connections[i].databaseName == databaseName) {
                return this.connections[i];
            }
        }
        return null;
    }


    /**
     * Start listening on database console outputs
     * @param {databaseName} Database name
     */
    this.startListener = function (databaseName) {

        if (this.isWebsocketSupported == false) return;

        try {

            var connection = { socket: null, databaseName: databaseName };

            var errorHeader = "Websocket error";

            connection.socket = new WebSocket("ws://" + location.host + "/__" + databaseName + "/console/ws");


            connection.socket.onopen = function (evt) {
                self.connections.push(connection);
                connection.socket.send("PING");
            };

            connection.socket.onclose = function (evt) {
                //connection.socket = null;
                var index = self.connections.indexOf(connection);
                if (index > -1) {
                    self.connections.splice(index, 1);
                }
            };

            connection.socket.onmessage = function (evt) {

                $log.warn("Sending event message to " + self.listeners.length + " listeners");

                $rootScope.$apply(function () {
                    for (var i = 0; i < self.listeners.length ; i++) {
                        self.listeners[i].onEvent(evt.data);
                    }
                });

            };

            connection.socket.onerror = function (evt) {
                $log.error(errorHeader, evt);

                $rootScope.$apply(function () {

                    var messageObject = UtilsFactory.createErrorMessage(errorHeader, JSON.stringify(evt), null, null);

                    for (var i = 0; i < self.listeners.length ; i++) {
                        self.listeners[i].onError(messageObject);
                    }

                });

                $rootScope.$apply();


            };
        }
        catch (exception) {

            $log.error(errorHeader, exception);

            $rootScope.$apply(function () {

                var messageObject = UtilsFactory.createErrorMessage(errorHeader, exception.message, null, exception.stack);
                for (var i = 0; i < self.listeners.length ; i++) {
                    self.listeners[i].onError(messageObject);
                }


            });

        }
    }


    /**
     * Stop listening on database console outputs
     * @param {databaseName} Database name
     */
    this.stopListener = function (databaseName) {

        var connection = this.getConnection(databaseName);
        if (connection != null) {
            if (connection.socket.readyState == 0 || connection.socket.readyState == 2 || connection.socket.readyState == 3) return; // (0) CONNECTING // (2) CLOSING, (3) CLOSED

            connection.socket.close();

        }

    }


}]);



