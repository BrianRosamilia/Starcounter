/**
 * User Message Factory
 * Show messages to user (alert's, modal windows, etc..)
 */
adminModule.factory('UserMessageFactory', function ($dialog, $log) {
    var factory = {};

    /**
     * Show Error message modal popup
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
    return factory;

});

