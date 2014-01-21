/**
 * User Message Factory
 * Show messages to user (alert's, modal windows, etc..)
 */
adminModule.factory('UserMessageFactory', function ($dialog, $log) {
    var factory = {};

    /**
     * Show Error message modal popup
     * @param {title} Title
     * @param {message} Message
     * @param {helpLink} HelpLink Url
     * @param {stackTrace} stackTrace
     */
    factory.showErrorMessage = function (title, message, helpLink, stackTrace) {

        var opts = {
            backdrop: true,
            keyboard: true,
            backdropClick: true,
            templateUrl: "app/partials/errorMessage.html",
            controller: 'UserErrorMessageCtrl',
            data: { header: title, message: message, stackTrace: stackTrace, helpLink: helpLink }
        };

        var d = $dialog.dialog(opts);
        d.open();
    };


    /**
     * Show Message box
     * @param {title} Title
     * @param {message} Message
     * @param {buttons} Buttons, Example: [{ result: 0, label: 'Ok', cssClass: 'btn' }, { result: 1, label: 'Cancel', cssClass: 'btn-danger' }]
     * @param {responseCallback} Response Callback function
     */
    factory.showMessageBox = function (title, message, buttons, responseCallback) {

        $dialog.messageBox(title, message, buttons)
          .open()
          .then(function (result) {

              if (typeof (responseCallback) == "function") {
                  responseCallback(result);
              }

          });

    }

    return factory;

});

