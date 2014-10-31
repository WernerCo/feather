﻿(function ($) {
    angular.module('sfSelectors')
        .directive('sfTaxonSelector', ['flatTaxonService', function (flatTaxonService) {
            // Tags Id
            var defaultTaxonomyId = "cb0f3a19-a211-48a7-88ec-77495c0f5374";
            var emptyGuid = "00000000-0000-0000-0000-000000000000";

            return {
                require: '^sfListSelector',
                restrict: 'A',
                link: {
                    pre: function (scope, element, attrs, ctrl) {
                        //var taxonomyId = ctrl.$scope.taxonomyId;
                        var taxonomyId = attrs.taxonomyId;
                        if (!taxonomyId || taxonomyId === emptyGuid) {
                            taxonomyId = defaultTaxonomyId;
                        }

                        ctrl.getItems = function (skip, take, search, frontendLanguages) {
                            return flatTaxonService.getTaxons(taxonomyId, skip, take, search, frontendLanguages);
                        };

                        ctrl.getSpecificItems = function (ids) {
                            return flatTaxonService.getSpecificItems(taxonomyId, ids);
                        };

                        ctrl.selectorType = 'TaxonSelector';
                        ctrl.dialogTemplateUrl = 'Selectors/sf-taxon-selector.html';
                        ctrl.closedDialogTemplateUrl = attrs.multiselect ? 'Selectors/list-group-selection.html' : 'Selectors/bubbles-selection.html';
                        ctrl.$scope.dialogTemplateId = 'sf-taxon-selector-template';
                    }
                }
            };
        }]);
})(jQuery);