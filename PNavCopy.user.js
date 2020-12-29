// ==UserScript==
/* globals $, GM_info */
// eslint-disable-next-line multiline-comment-style
// @id             pnavcopy@maxetmoritz
// @name           IITC plugin: Copy PokeNav Command
// @category       Misc
// @downloadURL    https://raw.github.com/MaxEtMoritz/PNavCopy/main/PNavCopy.user.js
// @author         MaxEtMoritz
// @version        1.5.2
// @namespace      https://github.com/MaxEtMoritz/PNavCopy
// @description    Copy portal info to clipboard or send it to Discord in the format the PokeNav Discord bot needs.
// @include        http*://intel.ingress.com/*
// @grant          none
// ==/UserScript==

// original Plug-In is from https://gitlab.com/ruslan.levitskiy/iitc-mobile/-/tree/master, the License of this project is provided below:

/*
 * ISC License
 *
 * Copyright © 2013 Stefan Breunig
 *
 * Permission to use, copy, modify, and/or distribute this software for
 * any purpose with or without fee is hereby granted, provided that the
 * above copyright notice and this permission notice appear in all copies.
 *
 * THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL
 * WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE
 * AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL
 * DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA
 * OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
 * TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
 * PERFORMANCE OF THIS SOFTWARE.
 */

function wrapper (plugin_info) {
  // ensure plugin framework is there, even if iitc is not yet loaded
  if (typeof window.plugin !== 'function') window.plugin = function () { };

  // PLUGIN START ////////////////////////////////////////////////////////

  // use own namespace for plugin
  window.plugin.pnav = function () { };
  // Language is set in setup() if not already present in localStorage.
  window.plugin.pnav.settings = {
    webhookUrl: '',
    name: window.PLAYER.nickname,
    radius: '',
    lat: '',
    lng: '',
    prefix: '$',
    language: null
  };
  var selectedGuid = null;
  var pNavData = {
    pokestop: {},
    gym: {}
  };
  const request = new XMLHttpRequest();

  const strings = {
    en: {
      alertAlreadyExported: 'This location has already been exported! If you are sure this is not the case, the creation command has been copied to clipboard for you. If this happens too often, try to reset the export state in the settings.',
      alertExportRunning: 'Settings not saved because Export was running. Pause the Export and then try again!',
      alertLanguageAfterReload: 'The new language settings will be fully in effect after a page reload.',
      alertNoModifications: 'No modifications detected!',
      alertOutsideArea: 'This location is outside the specified Community Area!',
      alertProblemPogoTools: 'There was a problem reading the Pogo Tools Data File.',
      btnBulkExportGymsText: 'Export all Pogo Tools Gyms',
      btnBulkExportGymsTitle: 'Grab the File where all Gyms are stored by PoGo Tools and send them one by one via WebHook. This can take much time!',
      btnBulkExportStopsText: 'Export all Pogo Tools Stops',
      btnBulkExportStopsTitle: 'Grab the File where all Stops are stored by PoGo Tools and send them one by one via WebHook. This can take much time!',
      btnBulkModifyText: 'Check for Modifications',
      btnBulkModifyTitle: 'Check if the Pogo Tools Data was modified and start Upload process of modifications',
      btnEraseHistoryTextDefault: 'Delete Location Export History',
      btnEraseHistoryTextSuccess: 'Deleted!',
      btnEraseHistoryTitle: 'Delete all collected Export History.',
      btnSkipText: 'Skip one',
      bulkExportProgressButtonText: 'Pause',
      bulkExportProgressButtonTitle: 'Store Progress locally and stop Exporting. If you wish to restart, go to Settings and click the Export Button again.',
      bulkExportProgressTitle: 'PokeNav Bulk Export Progress',
      exportProgressBarDescription: 'Progress:',
      exportStateTextExporting: 'Exporting...',
      exportStateTextReady: 'Export Ready!',
      exportTimeRemainingDescription: 'Time remaining: ',
      lblErrorCnText: 'Invalid Coordinate Format! Please input them like 00.0...00, 00.0...00!',
      lblErrorPfText: 'Prefix must be only one Character!',
      lblErrorRdText: 'Invalid Radius! Please check if it is a valid Number!',
      lblErrorWHText: 'Invalid URL! Please delete or correct it!',
      Modification: 'Modification ',
      of: ' of ',
      pnavCenterDescription: 'Community Center:',
      pnavCenterTitle: `Paste the Center Coordinate of your Community here (you can view it typing ${window.plugin.pnav.settings.prefix}show settings in Admin Channel)`,
      pNavChangesMadeDescription: 'The following has changed:',
      pnavCodenameDescription: 'Name:',
      pnavCodenameTitle: 'The Name that will be displayed if you send to the PokeNav channel. Default is your Ingess Codename.',
      PNavExDescription: 'Ex Gym',
      PNavGymDescription: 'Gym',
      pnavhookurlDescription: 'Discord WebHook URL:',
      pnavhookurlTitle: "Paste the URL of the WebHook you created in your Server's Admin Channel here. If left blank, the Commands are copied to Clipboard.",
      pnavLanguageDescription: 'Language:',
      pNavModCommandText: [
        {
          send: {
            false: 'Copy',
            true: 'Send'
          }
        },
        ' Modification Command'
      ],
      pNavModCommandTitleDisabled: [
        'You must input the PokeNav location ID before you can ',
        {
          send: {
            false: 'copy',
            true: 'send'
          }
        },
        ' the modification command!'
      ],
      pNavModCommandTitleEnabled: [
        {
          send: {
            false: 'Copies',
            true: 'Sends'
          }
        },
        ' the modification command.'
      ],
      pNavmodDialogTitle: 'PokeNav Modification(s)',
      PNavNoneDescription: 'None',
      pNavOldPoiNameDescription: 'The following POI was modified:',
      pNavPoiIdDescription: 'PokeNav ID:',
      pNavPoiInfoText: [
        {
          send: {
            false: 'Copy',
            true: 'Send'
          }
        },
        ' POI Info Command'
      ],
      pNavPoiInfoTitle: [
        {
          send: {
            false: 'Copies',
            true: 'Sends'
          }
        },
        ' the POI Information Command for the POI.'
      ],
      pnavprefixDescription: 'PokeNav Prefix:',
      pnavprefixTitle: 'Input the Prefix for the PokeNav Bot here. Default Prefix is $.',
      pnavRadiusDescription: 'Community Radius (Km):',
      pnavRadiusTitle: 'Enter the specified Community Radius in kilometers here.',
      pnavsettingsTitle: 'PokeNav Settings',
      PNavStopDescription: 'Stop',
      PogoButtonsText: [
        {
          send: {
            false: 'Copy',
            true: 'Send to'
          }
        },
        ' PokeNav'
      ],
      PogoButtonsTitle: [
        {
          send: {
            false: 'Copy',
            true: 'Send'
          }
        },
        ' the Location create Command to ',
        {
          send: {
            false: 'Clipboard',
            true: 'Discord via WebHook'
          }
        }
      ],
      pokeNavSettingsText: 'PokeNav Settings',
      pokeNavSettingsTitle: 'Configure PokeNav'
    },
    de: {
      alertAlreadyExported: 'Dieser POI wurde schon exportiert! Wenn dies mit Sicherheit nicht der Fall ist, wurde das Kommando zum Erstellen in die Zwischenablage kopiert. Passiert dies zu häufig, versuche, den Export-Status in den Einstellungen zurückzusetzen.',
      alertExportRunning: 'Die Einstellungen wurden nicht gespeichert, da der Daten-Export läuft. Pausiere den Export und versuche es noch mal!',
      alertLanguageAfterReload: 'Die neuen Spracheinstellungen werden vollständig erst nach erneutem Laden der Seite wirksam!',
      alertNoModifications: 'Keine Änderungen gefunden!',
      alertOutsideArea: 'Dieser POI liegt nicht in den angegebenen Community-Grenzen!',
      alertProblemPogoTools: 'Es ist ein Problem beim Lesen der Pogo-Tools-Daten aufgetreten!',
      btnBulkExportGymsText: 'Exportiere alle Pogo Tools Arenen',
      btnBulkExportGymsTitle: 'Exportiere alle Arenen aus Pogo Tools eine nach der Anderen über den angegebenen WebHook. Dies kann eine Weile dauern!',
      btnBulkExportStopsText: 'Exportiere alle Pogo Tools Stops',
      btnBulkExportStopsTitle: 'Exportiere alle Pokestops aus Pogo Tools einer nach dem Anderen über den angegebenen WebHook. Dies kann eine Weile dauern!',
      btnBulkModifyText: 'Prüfe auf Änderungen',
      btnBulkModifyTitle: 'Prüft die Pogo-Tools-Daten auf Änderungen und beginnt den Upload-Prozess der Änderungen.',
      btnEraseHistoryTextDefault: 'Lösche Export-Historie',
      btnEraseHistoryTextSuccess: 'Gelöscht!',
      btnEraseHistoryTitle: 'Lösche die gesamte bisher gesammelte Export-Historie.',
      btnSkipText: 'Änderung überspringen',
      bulkExportProgressButtonText: 'Pause',
      bulkExportProgressButtonTitle: 'Speichert den Fortschritt lokal und beendet den Export. Starten Sie zum Fortsetzen des Exports diesen in den Einstellungen neu.',
      bulkExportProgressTitle: 'Fortschritt des PokeNav Massen-Exports',
      exportProgressBarDescription: 'Fortschritt:',
      exportStateTextExporting: 'Exportiere...',
      exportStateTextReady: 'Export Abgeschlossen!',
      exportTimeRemainingDescription: 'Verbleibende Zeit: ',
      lblErrorCnText: 'Ungültiges Koordinaten-Format! Bitte geben Sie sie wie Folgt ein: 00.0...00, 0.0...00!',
      lblErrorPfText: 'Präfix darf nur ein Zeichen sein!',
      lblErrorRdText: 'Ungüliger Radius! Bitte überprüfen Sie, ob Sie eine gültige Zahl eingegeben haben!',
      lblErrorWHText: 'Ungültige URL! Bitte löschen oder korrigieren Sie sie!',
      Modification: 'Änderung ',
      of: ' von ',
      pnavCenterDescription: 'Community-Mittelpunkt:',
      pnavCenterTitle: `Fügen Sie die Mittelpunkt-Koordinate Ihrer Community hier ein. Sie können sie abrufen, indem Sie &quot;${window.plugin.pnav.settings.prefix}show settings&quot; in den PokeNav Administratoren-Kanal eigeben.`,
      pNavChangesMadeDescription: 'Folgendes wurde geändert:',
      pnavCodenameDescription: 'Name:',
      pnavCodenameTitle: 'Der Name, der beim Senden über den WebHook angezeigt wird. Standardmäßig ist es Ihr Ingress-Codename.',
      PNavExDescription: 'Ex-Arena',
      PNavGymDescription: 'Arena',
      pnavhookurlDescription: 'Discord WebHook URL:',
      pnavhookurlTitle: 'Geben Sie die URL des WebHooks, den Sie in Ihrem Administrations-Channel angelegt haben, hier ein. Ist dieses Feld leer, werden die Kommandos in die Zwischenablage kopiert.',
      pnavLanguageDescription: 'Sprache:',
      pNavModCommandText: [
        {
          send: {
            false: 'Kopiere',
            true: 'Sende'
          }
        },
        ' Änderungs-Befehl'
      ],
      pNavModCommandTitleDisabled: [
        'Sie müssen die PokeNav POI-ID eingeben bevor Sie den Änderungs-Befehl ',
        {
          send: {
            false: 'kopieren',
            true: 'senden'
          }
        },
        ' können!'
      ],
      pNavModCommandTitleEnabled: [
        {
          send: {
            false: 'Kopiert',
            true: 'Sendet'
          }
        },
        ' den Änderungs-Befehl.'
      ],
      pNavmodDialogTitle: 'PokeNav Änderung(en)',
      PNavNoneDescription: 'Nichts',
      pNavOldPoiNameDescription: 'Folgender POI wurde geändert:',
      pNavPoiIdDescription: 'PokeNav ID:',
      pNavPoiInfoText: [
        {
          send: {
            false: 'Kopiere',
            true: 'Sende'
          }
        },
        ' POI Informations-Befehl'
      ],
      pNavPoiInfoTitle: [
        {
          send: {
            false: 'Kopiert',
            true: 'Sendet'
          }
        },
        ' den POI Informations-Befehl für diesen POI.'
      ],
      pnavprefixDescription: 'PokeNav Präfix:',
      pnavprefixTitle: 'Geben Sie hier die Präfix des PokeNav-Bots ein. Die Standard-Präfix ist $.',
      pnavRadiusDescription: 'Community-Radius (Km):',
      pnavRadiusTitle: 'Geben Sie hier den Radius ihrer Community in Kilometern ein.',
      pnavsettingsTitle: 'PokeNav-Einstellungen',
      PNavStopDescription: 'Stop',
      PogoButtonsText: [
        {
          send: {
            false: 'Kopiere',
            true: 'An'
          }
        },
        ' PokeNav',
        {send: {true: ' senden',
          false: ''}}
      ],
      PogoButtonsTitle: [
        {
          send: {
            false: 'Kopiere',
            true: 'Sende'
          }
        },
        ' den POI-Befehl ',
        {
          send: {
            false: 'in die Zwischenablage.',
            true: 'an Discord über den WebHook.'
          }
        }
      ],
      pokeNavSettingsText: 'PokeNav-Einstellungen',
      pokeNavSettingsTitle: 'Konfigurieren Sie PokeNav'
    }
  };

  function detectLanguage () {
    let lang = navigator.language;
    console.log(lang);
    lang = lang.split('-')[0].toLowerCase();
    if (Object.keys(strings).includes(lang)) {
      return lang;
    } else {
      return 'en';
    }
  }

  window.plugin.pnav.getString = getString;

  function getString (id, options) {
    if (window.plugin.pnav.settings.language && strings[window.plugin.pnav.settings.language] && (strings[window.plugin.pnav.settings.language])[id]) {
      var string = (strings[window.plugin.pnav.settings.language])[id];
      if (!(typeof string === 'string')) {
        return parseNestedString(string, options);
      } else {
        return string;
      }
    } else if (strings.en && strings.en[id]) {
      var string = strings.en[id];
      if (!(typeof string === 'string')) {
        return parseNestedString(string, options);
      } else {
        return string;
      }
    } else {
      return id;
    }
  }

  function parseNestedString (object, options) {
    if (typeof object === 'string' || object instanceof String) {
      if (object.length > 1 && object.startsWith('#')) {
        return getString(object.substring(1), options);
      } else {
        return object;
      }
    } else if (object instanceof Array) {
      let newString = '';
      object.forEach(function (entry) {
        newString += parseNestedString(entry, options);
      });
      return newString;
    } else if (typeof object === 'object' && Object.keys(object).length > 0) {
      const optionName = Object.keys(object)[0];
      var decision = object[optionName];
      if (options && Object.keys(options).includes(optionName) && Object.keys(decision).includes(String(options[optionName]))) {
        const optionValue = String(options[optionName]);
        return parseNestedString(decision[optionValue]);
      } else if (Object.keys(decision).includes('default')) {
        return parseNestedString(decision.default);
      } else if (Object.keys(decision).length > 0) {
        return parseNestedString(decision[Object.keys(decision)[0]]);
      } else {
        return '';
      }
    } else {
      return '';
    }
  }

  window.plugin.pnav.copy = function () {
    var input = $('#copyInput');
    if (window.selectedPortal) {
      var portal = window.portals[selectedGuid];
      // escaping Backslashes and Hyphens in Portal Names
      /** @type {string} */
      var name = portal.options.data.title
        .replaceAll('"', '\\"');
      var latlng = portal.getLatLng();
      var lat = latlng.lat;
      var lng = latlng.lng;
      var opt = ' ';
      var type = 'none';
      var isEx;

      /** @type {string} */
      var prefix = window.plugin.pnav.settings.prefix;
      if (window.plugin.pogo) {
        if ($('.pogoStop span').css('background-position') == '100% 0%') {
          type = 'pokestop';
        } else if ($('.pogoGym span').css('background-position') == '100% 0%') {
          type = 'gym';
          if ($('#PogoGymEx').prop('checked') == true) {
            opt += '"ex_eligible: 1"';
            isEx = true;
          }
        }
      } else {
        if (document.getElementById('PNavEx').checked) {
          type = 'gym';
          opt += '"ex_eligible: 1"';
          isEx = true;
        } else if (document.getElementById('PNavGym').checked) {
          type = 'gym';
        } else if (document.getElementById('PNavStop').checked) {
          type = 'pokestop';
        }
      }
      if (
        window.plugin.pnav.settings.lat &&
        window.plugin.pnav.settings.lng &&
        window.plugin.pnav.settings.radius &&
        checkDistance(lat, lng, window.plugin.pnav.settings.lat, window.plugin.pnav.settings.lng) >
        window.plugin.pnav.settings.radius
      ) {
        alert(getString('alertOutsideArea'));
      } else {
        var changes = checkForSingleModification({
          type,
          guid: selectedGuid,
          name,
          isEx,
          lat,
          lng
        });
        if (changes) {
          window.plugin.pnav.bulkModify([changes]);
        } else if (
          type !== 'none' && (pNavData[type])[selectedGuid]
        ) {
          alert(getString('alertAlreadyExported'));
          input.show();
          input.val(`${prefix}create poi ${type} "${name}" ${lat} ${lng}${opt}`);
          copyfieldvalue('copyInput');
          input.hide();
        } else if (type !== 'none') {
          if (window.plugin.pnav.settings.webhookUrl) {
            sendMessage(`${prefix}create poi ${type} "${name}" ${lat} ${lng}${opt}`);
            console.log('sent!');
          } else {
            input.show();
            input.val(`${prefix}create poi ${type} "${name}" ${lat} ${lng}${opt}`);
            copyfieldvalue('copyInput');
            input.hide();
          }
          let pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : {};
          if (pogoData[`${type}s`] && ((pogoData[`${type}s`])[selectedGuid])) {
            (pNavData[type])[selectedGuid] = (pogoData[`${type}s`])[selectedGuid];
          } else {
            var newObject = {
              'guid': String(selectedGuid),
              'name': String(portal.options.data.title),
              'lat': String(lat),
              'lng': String(lng)
            };
            if ($('#PNavEx').prop('checked')) {
              newObject.isEx = true;
            }
            (pNavData[type])[selectedGuid] = newObject;
          }
          saveToLocalStorage();
        }
      }
    }
  };

  window.plugin.pnav.showSettings = function () {
    var validURL = '^https?://discord(app)?.com/api/webhooks/[0-9]*/.*';
    var html = `
        <p>
          <label>
            ${getString('pnavLanguageDescription')}
            <select id="pnavLanguage" onchange="window.plugin.pnav.settings.language = this.value;alert(window.plugin.pnav.getString('alertLanguageAfterReload'));"/>
          </label>
        </p>
        <p id="prefix">
          <label title="${getString('pnavprefixTitle')}">
            ${getString('pnavprefixDescription')}
            <input type="text" id="pnavprefix" pattern="^.$" value="${window.plugin.pnav.settings.prefix}" placeholder="$" style="width:15px"/>
          </label>
        </p>
        <p id="webhook">
          <label title="${getString('pnavhookurlTitle')}">
            ${getString('pnavhookurlDescription')}
            <input type="url" style="width:100%" id="pnavhookurl" value="${window.plugin.pnav.settings.webhookUrl}" pattern="${validURL}"/>
          </label>
        </p>
        <p>
          <Label title="${getString('pnavCodenameTitle')}">
            ${getString('pnavCodenameDescription')}
            <input id="pnavCodename" type="text" placeholder="${window.PLAYER.nickname}" value="${window.plugin.pnav.settings.name}"/>
          </label>
        </p>
        <p>
          <label id="center" title="${getString('pnavCenterTitle')}">
          ${getString('pnavCenterDescription')}
          <input id="pnavCenter" style="width:140px" type="text" pattern="^-?&#92;d?&#92;d(&#92;.&#92;d+)?, -?1?&#92;d?&#92;d(&#92;.&#92;d+)?$" value="${window.plugin.pnav.settings.lat != '' ? `${window.plugin.pnav.settings.lat}, ${window.plugin.pnav.settings.lng}` : ''}"/>
          </label>
          <br>
          <label id="radius" title="${getString('pnavRadiusTitle')}">
          ${getString('pnavRadiusDescription')}
          <input id="pnavRadius" style="width:41px;appearance:textfield;-moz-appearance:textfield;-webkit-appearance:textfield" type="number" min="0" step="0.001" value="${window.plugin.pnav.settings.radius}"/>
          </label>
        </p>
        <p><button type="Button" id="btnEraseHistory" style="width:100%" title="${getString('btnEraseHistoryTitle')}" onclick="
          window.plugin.pnav.deleteExportState();
          $(this).css('color','green');
          $(this).css('border','1px solid green')
          $(this).text('${getString('btnEraseHistoryTextSuccess')}');
          setTimeout(function () {
            if($('#btnEraseHistory').length > 0){
              $('#btnEraseHistory').css('color', '');
              $('#btnEraseHistory').css('border', '');
              $('#btnEraseHistory').text('${getString('btnEraseHistoryTextDefault')}');
            }
          }, 1000);
          return false;
        ">${getString('btnEraseHistoryTextDefault')}</button></p>
        `;
    if (window.plugin.pogo && window.plugin.pnav.settings.webhookUrl) {
      html += `
            <p><button type="Button" id="btnBulkExportGyms" style="width:100%" title="${getString('btnBulkExportGymsTitle')}" onclick="window.plugin.pnav.bulkExport('gym');return false;">${getString('btnBulkExportGymsText')}</button></p>
            <p><button type="Button" id="btnBulkExportStops" style="width:100%" title="${getString('btnBulkExportStopsTitle')}" onclick="window.plugin.pnav.bulkExport('pokestop');return false;">${getString('btnBulkExportStopsText')}</button></p>
            `;
    }
    if (window.plugin.pogo) {
      html += `
      <p><button type="Button" id="btnBulkModify" style="width:100%" title="${getString('btnBulkModifyTitle')}" onclick="window.plugin.pnav.bulkModify();return false;">${getString('btnBulkModifyText')}</button></p>
      `;
    }

    const container = window.dialog({
      id: 'pnavsettings',
      width: 'auto',
      height: 'auto',
      html,
      title: getString('pnavsettingsTitle'),
      buttons: {
        OK () {
          let allOK = true;
          
          if (
            !$('#pnavhookurl').val() ||
            new RegExp(validURL).test($('#pnavhookurl').val())
          ) {
            window.plugin.pnav.settings.webhookUrl = $('#pnavhookurl').val();
            if ($('#lblErrorWH').length > 0) {
              $('#lblErrorWH').remove();
            }
          } else {
            if ($('#lblErrorWH').length == 0) {
              $('#webhook').after(`<label id="lblErrorWH" style="color:red">${getString('lblErrorWHText')}</label>`);
            }
            allOK = false;
          }
          if ($('#pnavprefix').val() && $('#pnavprefix').val().length == 1) {
            window.plugin.pnav.settings.prefix = $('#pnavprefix').val();
            if ($('#lblErrorPf').length > 0) {
              $('#lblErrorPf').remove();
            }
          } else if (!$('#pnavprefix').val()) {
            window.plugin.pnav.settings.prefix = '$';
            if ($('#lblErrorPf').length > 0) {
              $('#lblErrorPf').remove();
            }
          } else {
            allOK = false;
            if ($('#lblErrorPf').length == 0) {
              $('#prefix').after(`<label id="lblErrorPf" style="color:red">${getString('lblErrorPfText')}</label>`);
            }
          }
          if (!$('#pnavRadius').val()) {
            window.plugin.pnav.settings.radius = '';
            if ($('#lblErrorRd').length > 0) {
              $('#lblErrorRd').remove();
            }
          } else if (
            new RegExp('^\\d+(\\.\\d+)?$').test($('#pnavRadius').val()) &&
            !Number.isNaN(parseFloat($('#pnavRadius').val()))
          ) {
            window.plugin.pnav.settings.radius = parseFloat($('#pnavRadius').val());
            if ($('#lblErrorRd').length > 0) {
              $('#lblErrorRd').remove();
            }
          } else {
            if ($('#lblErrorRd').length == 0) {
              $('#radius').after(`<label id="lblErrorRd" style="color:red"><br>${getString('lblErrorRdText')}</label>`);
            }
            allOK = false;
          }
          if (!$('#pnavCenter').val()) {
            window.plugin.pnav.settings.lat = '';
            window.plugin.pnav.settings.lng = '';
            if ($('#lblErrorCn').length > 0) {
              $('#lblErrorCn').remove();
            }
          } else {

            /** @type {string[]} */
            let arr = $('#pnavCenter').val()
              .split(', ');
            let lat = arr[0] ? parseFloat(arr[0]) : NaN;
            let lng = arr[1] ? parseFloat(arr[1]) : NaN;
            if (
              !Number.isNaN(lat) &&
              !Number.isNaN(lng) &&
              new RegExp('^-?\\d?\\d(\\.\\d+)?, -?1?\\d?\\d(\\.\\d+)?$').test($('#pnavCenter').val()) &&
              lat >= -90 &&
              lat <= 90 &&
              lng >= -180 &&
              lng <= 180
            ) {
              window.plugin.pnav.settings.lat = lat;
              window.plugin.pnav.settings.lng = lng;
              if ($('#lblErrorCn').length > 0) {
                $('#lblErrorCn').remove();
              }
            } else {
              if ($('#lblErrorCn').length == 0) {
                $('#center').after(`<label id="lblErrorCn" style="color:red"><br>${getString('lblErrorCnText')}</label>`);
              }
              allOK = false;
            }
          }
          if (!$('#pnavCodename').val()) {
            window.plugin.pnav.settings.name = window.PLAYER.nickname;
          } else {
            window.plugin.pnav.settings.name = $('#pnavCodename').val();
          }
          if (!window.plugin.pnav.timer) {
            if (allOK) {
              localStorage.setItem(
                'plugin-pnav-settings',
                JSON.stringify(window.plugin.pnav.settings)
              );
              container.dialog('close');
            }
          } else {
            alert(getString('alertExportRunning'));
            container.dialog('close');
          }
        }
      }
    });
    // unfocus all input fields to prevent the explanation tooltips to pop up
    $('input', container).blur();
    var languageDropdown = $('#pnavLanguage', container);
    Object.keys(strings).forEach(function (key) {
      languageDropdown.append(`<option value="${key}">${key}</option>`);
    });
    languageDropdown.val(window.plugin.pnav.settings.language);
  };

  window.plugin.pnav.deleteExportState = function () {
    if (localStorage['plugin-pnav-done-pokestop']) {
      localStorage.removeItem('plugin-pnav-done-pokestop');
    }
    if (localStorage['plugin-pnav-done-gym']) {
      localStorage.removeItem('plugin-pnav-done-gym');
    }
    pNavData.pokestop = {};
    pNavData.gym = {};
  };

  /**
   * saves the State of the Bulk Export to local Storage.
   * @param {object[]} data
   * @param {string} type
   * @param {number} index
   */
  function saveState (data, type, index) {
    const addToDone = data.slice(0, index);
    // console.log(addToDone);
    addToDone.forEach(function (object) {
      (pNavData[type])[object.guid] = object;
    });
    saveToLocalStorage();
  }

  window.plugin.pnav.bulkModify = function (changes) {
    const changeList = (changes && changes instanceof Array) ? changes : checkForModifications();
    if (changeList && changeList.length > 0) {
      // console.log(changeList);
      const send = Boolean(window.plugin.pnav.settings.webhookUrl);
      const html = `
        <label>${getString('Modification')}</label><label id=pNavModNrCur>1</label><label>${getString('of')}</label><label id="pNavModNrMax"/>
        <h3>
          ${getString('pNavOldPoiNameDescription')}
        </h3>
        <h3 id="pNavOldPoiName"/>
        <label>${getString('pNavChangesMadeDescription')}</label>
        <ul id="pNavChangesMade"/>
        <label>
          ${getString('pNavPoiIdDescription')}
          <input id="pNavPoiId" style="appearance:textfield;-moz-appearance:textfield;-webkit-appearance:textfield" type="number" min="0" step="1"/>
        </label>
        <br>
        <button type="Button" class="ui-button" id="pNavPoiInfo" title="${getString('pNavPoiInfoTitle', {send})}">
          ${getString('pNavPoiInfoText', {send})}
        </button>
        <button type="Button" class="ui-button" id="pNavModCommand" title="${getString('pNavModCommandTitleDisabled', {send})}" style="color:darkgray;cursor:default;text-decoration:none">
          ${getString('pNavModCommandText', {send})}
        </button>
      `;
      const modDialog = window.dialog({
        id: 'pNavmodDialog',
        title: getString('pNavmodDialogTitle'),
        html,
        width: 'auto',
        height: 'auto',
        buttons: {
          Skip: {
            id: 'btnSkip',
            text: getString('btnSkipText'),
            click () {
              i++;
              if (i == changeList.length) {
                modDialog.dialog('close');
              } else {
                poi = changeList[i];
                updateUI(modDialog, poi, i);
              }
            }
          }
        }
      });
      var i = 0;
      var poi = changeList[i];
      $('#pNavPoiInfo', modDialog).on('click', function () {
        if (window.plugin.pnav.settings.webhookUrl) {
          sendMessage(`${window.plugin.pnav.settings.prefix}${poi.oldType}-info ${poi.oldName}`);
        } else {
          const input = $('#copyInput');
          input.show();
          input.val(`${window.plugin.pnav.settings.prefix}${poi.oldType}-info ${poi.oldName}`);
          copyfieldvalue('copyInput');
          input.hide();
        }
      });
      $('#pNavPoiId', modDialog).on('input', function (e) {
        // console.log(e);
        const valid = e.target.validity.valid;
        const value = e.target.valueAsNumber;
        if (valid && value && value > 0) {
          $('#pNavModCommand', modDialog).prop('style', '');
          $('#pNavModCommand', modDialog).prop('title', getString('pNavModCommandTitleEnabled', {send}));
        } else {
          $('#pNavModCommand', modDialog).css('color', 'darkgray');
          $('#pNavModCommand', modDialog).css('cursor', 'default');
          $('#pNavModCommand', modDialog).css('text-decoration', 'none');
          $('#pNavModCommand', modDialog).css('border', '1px solid darkgray');
          $('#pNavModCommand', modDialog).prop('title', getString('pNavModCommandTitleDisabled', {send}));
        }
      });
      $('#pNavModCommand', modDialog).on('click', function () {
        if ($('#pNavPoiId', modDialog).val() && new RegExp('^\\d*$').test($('#pNavPoiId', modDialog).val())) {
          sendModCommand($('#pNavPoiId', modDialog).val(), poi);
          updateDone(poi);
          i++;
          if (i == changeList.length) {
            modDialog.dialog('close');
          } else {
            poi = changeList[i];
            updateUI(modDialog, poi, i);
          }
        }
      });
      $('#pNavModNrMax', modDialog).text(changeList.length);
      updateUI(modDialog, poi, i);
    } else {
      alert(getString('alertNoModifications'));
    }
  };

  function updateDone (poi) {
    const pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : {};
    const pogoGyms = pogoData.gyms ? pogoData.gyms : {};
    const pogoStops = pogoData.stops ? pogoData.stops : {};
    if (Object.keys(pogoStops).includes(poi.guid)) {
      pNavData.pokestop[poi.guid] = pogoStops[poi.guid];
      if (Object.keys(pNavData.gym).includes(poi.guid)) {
        delete pNavData.gym[poi.guid];
      }
    } else if (Object.keys(pogoGyms).includes(poi.guid)) {
      pNavData.gym[poi.guid] = pogoGyms[poi.guid];
      if (Object.keys(pNavData.pokestop).includes(poi.guid)) {
        delete pNavData.pokestop[poi.guid];
      }
    } else {
      if (poi.oldType === 'gym') {
        delete pNavData.gym[poi.guid];
      } else {
        delete pNavData.pokestop[poi.guid];
      }
    }
    saveToLocalStorage();
  }

  function updateUI (dialog, poi, i) {
    $('#pNavOldPoiName', dialog).text(poi.oldName);
    $('#pNavModNrCur', dialog).text(i + 1);
    $('#pNavChangesMade', dialog).empty();
    for (const [
      key,
      value
    ] of Object.entries(poi)) {
      if (key !== 'oldName' && key !== 'oldType' && key !== 'guid') {
        $('#pNavChangesMade', dialog).append(`<li>${key} => ${value}</li>`);
      }
    }
    $('#pNavPoiId', dialog).val('');
    $('#pNavModCommand', dialog).css('color', 'darkgray');
    $('#pNavModCommand', dialog).css('border', '1px solid darkgray');
    $('#pNavModCommand', dialog).css('cursor', 'default');
    $('#pNavModCommand', dialog).css('text-decoration', 'none');
    $('#pNavModCommand', dialog).prop('title', getString('pNavModCommandTitleDisabled', {send: Boolean(window.plugin.pnav.settings.webhookUrl)}));
  }

  function sendModCommand (pNavId, changes) {
    let command = '';
    if (changes.type && changes.type === 'none') {
      command = `${window.plugin.pnav.settings.prefix}delete poi ${pNavId}`;
    } else {
      command = `${window.plugin.pnav.settings.prefix}update poi ${pNavId}`;
      for (const [
        key,
        value
      ] of Object.entries(changes)) {
        if (key !== 'oldType' && key !== 'oldName' && key !== 'guid') {
          if (key === 'name') {
            command += ` "${key}: ${value.replaceAll('"', '\\"')}"`;
          } else {
            command += ` "${key}: ${value}"`;
          }
        }
      }
    }
    if (window.plugin.pnav.settings.webhookUrl) {
      sendMessage(command);
    } else {
      const input = $('#copyInput');
      input.show();
      input.val(command);
      copyfieldvalue('copyInput');
      input.hide();
    }
  }

  function checkForModifications () {
    const pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : {};
    const pogoStops = (pogoData && pogoData.pokestops) ? pogoData.pokestops : {};
    // console.log(pogoStops);
    const keysStops = Object.keys(pogoStops);
    const pogoGyms = pogoData && pogoData.gyms ? pogoData.gyms : {};
    const keysGyms = Object.keys(pogoGyms);
    var changeList = [];
    if (pogoData) {
      Object.values(pNavData.pokestop).forEach(function (stop) {
        let detectedChanges = {};
        let originalData;
        if (!keysStops.includes(stop.guid)) {
          if (keysGyms.includes(stop.guid)) {
            detectedChanges.type = 'gym';
            originalData = pogoGyms[stop.guid];
            if (originalData.isEx) {
              detectedChanges.ex_eligible = 1;
            }
          } else {
            detectedChanges.type = 'none';
          }
        } else {
          originalData = pogoStops[stop.guid];
        }
        // compare data
        if (originalData) {
          if (originalData.name !== stop.name) {
            detectedChanges.name = originalData.name;
          }
          // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in Pogo Tools. Maybe there's a bug with that...
          if (originalData.lat != stop.lat) {
            detectedChanges.latitude = originalData.lat;
          }
          // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in Pogo Tools. Maybe there's a bug with that...
          if (originalData.lng != stop.lng) {
            detectedChanges.longitude = originalData.lng;
          }
        }
        if (Object.keys(detectedChanges).length > 0) {
          detectedChanges.oldName = stop.name;
          detectedChanges.oldType = 'stop';
          detectedChanges.guid = stop.guid;
          changeList.push(detectedChanges);
        }
      });
      Object.values(pNavData.gym).forEach(function (gym) {
        let detectedChanges = {};
        let originalData;
        if (!keysGyms.includes(gym.guid)) {
          if (keysStops.includes(gym.guid)) {
            detectedChanges.type = 'pokestop';
            originalData = pogoStops[gym.guid];
          } else {
            detectedChanges.type = 'none';
          }
        } else {
          originalData = pogoGyms[gym.guid];
        }
        // compare data
        if (originalData) {
          if (originalData.name !== gym.name) {
            detectedChanges.name = originalData.name;
          }
          // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in Pogo Tools. Maybe there's a bug with that...
          if (originalData.lat != gym.lat) {
            detectedChanges.latitude = originalData.lat;
          }
          // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in Pogo Tools. Maybe there's a bug with that...
          if (originalData.lng != gym.lng) {
            detectedChanges.longitude = originalData.lng;
          }
          if (originalData.isEx !== gym.isEx) {
            const newEx = originalData.isEx ? originalData.isEx : false;
            detectedChanges.ex_eligible = newEx ? 1 : 0;
          }
        }
        if (Object.keys(detectedChanges).length > 0) {
          detectedChanges.oldName = gym.name;
          detectedChanges.oldType = 'gym';
          detectedChanges.guid = gym.guid;
          changeList.push(detectedChanges);
        }
      });
    }
    return changeList;
  }

  function saveToLocalStorage () {
    localStorage['plugin-pnav-done-pokestop'] = JSON.stringify(pNavData.pokestop);
    localStorage['plugin-pnav-done-gym'] = JSON.stringify(pNavData.gym);
  }

  /**
   * Checks if a single Poi has been modified
   * @param {{
   * type: string,
   * guid: string,
   * name: string,
   * lat: (string|number),
   * lng: (string|number),
   * isEx:(boolean|undefined)
   * }} currentData
   * @return {{
   * oldType: string,
   * oldName: string,
   * guid: string,
   * latitude:(string|undefined),
   * longitude:(string|undefined),
   * name: (string|undefined),
   * type:(string|undefined),
   * ex_eligible:(number|undefined)
   * } | null} returns the found changes or null if none were found or a problem occurred.
   */
  function checkForSingleModification (currentData) {
    let changes = {};
    var savedData;
    if (pNavData.pokestop[currentData.guid]) {
      savedData = pNavData.pokestop[currentData.guid];
      savedData.type = 'pokestop';
    } else if (pNavData.gym[currentData.guid]) {
      savedData = pNavData.gym[currentData.guid];
      savedData.type = 'gym';
    } else {
      return null;
    }
    if (currentData.type !== savedData.type) {
      changes.type = currentData.type;
    }
    if (currentData.lat != savedData.lat) {
      changes.latitude = currentData.lat.toString();
    }
    if (currentData.lng != savedData.lng) {
      changes.longitude = currentData.lng.toString();
    }
    if (currentData.name != savedData.name) {
      changes.name = currentData.name;
    }
    if (currentData.isEx !== savedData.isEx) {
      changes.ex_eligible = currentData.isEx ? 1 : 0;
    }
    if (Object.keys(changes).length > 0) {
      changes.oldName = savedData.name;
      changes.oldType = savedData.type === 'pokestop' ? 'stop' : savedData.type;
      changes.guid = savedData.guid;
      return changes;
    } else {
      return null;
    }
  }

  /**
   * fetch previous data from local storage and add
   * @param {string} type - expected values pokestop or gym
   * @return {object[] | null} returns the data to export or null if Pogo Tools Data was not found.
   */
  function gatherExportData (type) {
    var pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : {};
    const modified = checkForModifications();
    const exportBlacklist = [];
    modified.forEach(function (modification) {
      if (modification.type && modification.type === type) {
        exportBlacklist.push(modification.guid);
      }
    });
    if (pogoData[`${type}s`]) {
      pogoData = Object.values(pogoData[`${type}s`]);
      // console.log(pogoData);
      const doneGuids = Object.keys(pNavData[type]);
      const distanceNotCheckable =
        !window.plugin.pnav.settings.lat ||
        !window.plugin.pnav.settings.lng ||
        !window.plugin.pnav.settings.radius;
      var exportData = pogoData.filter(function (object) {
        return (
          (!doneGuids || !doneGuids.includes(object.guid)) &&
          (distanceNotCheckable ||
            checkDistance(object.lat, object.lng, window.plugin.pnav.settings.lat, window.plugin.pnav.settings.lng) <= window.plugin.pnav.settings.radius) &&
          !exportBlacklist.includes(object.guid)
        );
      });
      return exportData;
    }
    return null;
  }

  /**
   * @param {string} type - the type of locations to export (expected values pokestop or gym)
   */
  window.plugin.pnav.bulkExport = function (type) {
    if (!window.plugin.pnav.timer) {
      var data = gatherExportData(type);
      if (!data) {
        alert(getString('alertProblemPogoTools'));
        return;
      }
      var i = 0;
      // Discord WebHook accepts 30 Messages in 60 Seconds.
      const wait = 2000;
      window.onbeforeunload = function () {
        saveState(data, type, i);
        return null;
      };
      window.plugin.pnav.timer = setInterval(function () {
        if ($('#exportProgressBar')) {
          $('#exportProgressBar').val(i);
        }
        if ($('#exportNumber')) {
          $('#exportNumber').text(i);
        }
        if ($('#exportTimeRemaining')) {
          $('#exportTimeRemaining').text((wait * (data.length - i)) / 1000);
        }
        if (i < data.length) {
          if (i % 10 == 0) {
            // sometimes save the state in case someone exits IITC Mobile without using the Back Button
            saveState(data, type, i);
          }
          var entry = data[i];
          let lat = entry.lat;
          let lng = entry.lng;
          // escaping Backslashes and Hyphens in Portal Names
          let name = entry.name
            .replaceAll('"', '\\"');
          let prefix = window.plugin.pnav.settings.prefix;
          let ex = Boolean(entry.isEx);
          let options = ex ? ' "ex_eligible: 1"' : '';
          var params = {
            username: window.plugin.pnav.settings.name,
            avatar_url: '',
            content: `${prefix}create poi ${type} "${name}" ${lat} ${lng}${options}`
          };
          request.open('POST', window.plugin.pnav.settings.webhookUrl);
          request.setRequestHeader('Content-type', 'application/json');
          request.onload = function () {
            if (request.status >= 200 && request.status < 300) {
              i++;
            } else {
              console.log(`status code ${request.status}`);
            }
          };
          request.send(JSON.stringify(params), false);
          // console.log('$create poi ' + type + ' "' + name + '" ' + lat + ' ' + lng + options);

        } else {
          $('#exportState').text(getString('exportStateTextReady'));
          okayButton.text('OK');
          okayButton.prop('title', '');
          saveState(data, type, i);
          clearInterval(window.plugin.pnav.timer);
          window.plugin.pnav.timer = null;
          window.onbeforeunload = null;
        }
      }, wait);
      // TODO fetch all strings below this to the top!
      var dialog = window.dialog({
        id: 'bulkExportProgress',
        html: `
              <h3 id="exportState">${getString('exportStateTextExporting')}</h3>
              <p>
                <label>
                  ${getString('exportProgressBarDescription')}
                  <progress id="exportProgressBar" value="0" max="${data.length}"/>
                </label>
              </p>
              <label id="exportNumber">0</label>
              <label>${getString('of')} ${data.length}</label>
              <br>
              <label>${getString('exportTimeRemainingDescription')}</label>
              <label id="exportTimeRemaining">${Math.round((wait * data.length) / 1000)}</label>
              <label>s</label>
        `,
        width: 'auto',
        title: getString('bulkExportProgressTitle'),
        buttons: {
          OK () {
            saveState(data, type, i);
            clearInterval(window.plugin.pnav.timer);
            window.plugin.pnav.timer = null;
            dialog.dialog('close');
          }
        }
      });

      // console.log(dialog);

      let thisDialog = dialog.parent();
      // console.log(thisDialog);
      var okayButton = $('.ui-button', thisDialog).not('.ui-dialog-titlebar-button');
      okayButton.text(getString('bulkExportProgressButtonText'));
      okayButton.prop(
        'title',
        getString('bulkExportProgressButtonTitle')
      );

      $('.ui-button.ui-dialog-titlebar-button-close', thisDialog).on(
        'click',
        function () {
          saveState(data, type, i);
          clearInterval(window.plugin.pnav.timer);
          window.plugin.pnav.timer = null;
        }
      );
    } else {
      console.log('Bulk Export already running!');
    }
  };

  /*
   *the idea of the following function was taken from https://stackoverflow.com/a/14561433
   *by User talkol (https://stackoverflow.com/users/1025458/talkol).
   *The License is CC BY-SA 4.0 (https://creativecommons.org/licenses/by-sa/4.0/)
   *The Code was slightly adapted.
   */
  /**
   * @param {number} lat1
   * @param {number} lon1
   * @param {number} lat2
   * @param {number} lon2
   * @return {number}
   */
  function checkDistance (lat1, lon1, lat2, lon2) {
    const R = 6371;
    var x1 = lat2 - lat1;
    var dLat = (x1 * Math.PI) / 180;
    var x2 = lon2 - lon1;
    var dLon = (x2 * Math.PI) / 180;
    var a =
      Math.sin(dLat / 2) * Math.sin(dLat / 2) +
      Math.cos((lat1 * Math.PI) / 180) *
      Math.cos((lat2 * Math.PI) / 180) *
      Math.sin(dLon / 2) *
      Math.sin(dLon / 2);
    var c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    var d = R * c;
    return d;
  }

  /**
   * @param {string} id - the id of the input field to copy from
   * @return {bool} - returns if copying was successful
   */
  function copyfieldvalue (id) {
    var field = document.getElementById(id);
    field.focus();
    field.setSelectionRange(0, field.value.length);
    field.select();
    return copySelectionText();
  }

  function copySelectionText () {
    var copysuccess;
    try {
      copysuccess = document.execCommand('copy');
    } catch (e) {
      copysuccess = false;
    }
    return copysuccess;
  }

  // source: Oscar Zanota on Dev.to (https://dev.to/oskarcodes/send-automated-discord-messages-through-webhooks-using-javascript-1p01)
  function sendMessage (msg) {
    var params = {
      username: window.plugin.pnav.settings.name,
      avatar_url: '',
      content: msg
    };
    request.open('POST', window.plugin.pnav.settings.webhookUrl);
    request.setRequestHeader('Content-type', 'application/json');
    request.send(JSON.stringify(params), false);
  }

  function waitForPogoButtons (mutationList, invokingObserver) {
    mutationList.forEach(function (mutation) {
      if (mutation.type === 'childList' && mutation.addedNodes) {
        // console.log(mutation.addedNodes);
        mutation.addedNodes.forEach((node) => {
          if (node.className == 'PogoButtons') {
            // console.log('there is PogoButtons!');
            $(node).after(`
             <a style="position:absolute;right:5px" title="${getString('PogoButtonsTitle', {send: Boolean(window.plugin.pnav.settings.webhookUrl)})}" onclick="window.plugin.pnav.copy();return false;" accesskey="p">${getString('PogoButtonsText', {send: Boolean(window.plugin.pnav.settings.webhookUrl)})}</a>
             `);
            $(node).css('display', 'inline');
            // we don't need to look for the class anymore because we just found what we wanted ;-)
            invokingObserver.disconnect();
          }
        });
      }
    });
  }

  function waitForPogoStatus (mutationList, invokingObserver) {
    mutationList.forEach(function (mutation) {
      if (mutation.type === 'childList' && mutation.addedNodes.length > 0) {
        $('.PogoStatus').append(`<a style="position:absolute;right:5px" onclick="window.plugin.pnav.copy();return false;">${getString('PogoButtonsText', {send: Boolean(window.plugin.pnav.settings.webhookUrl)})}</a>`);
        invokingObserver.disconnect();
      }
    });
  }

  var setup = function () {
    console.log('azaza');
    if (localStorage['plugin-pnav-settings']) {
      window.plugin.pnav.settings = JSON.parse(localStorage.getItem('plugin-pnav-settings'));
    }
    if (!window.plugin.pnav.settings.language) {
      window.plugin.pnav.settings.language = detectLanguage();
      localStorage['plugin-pnav-settings'] = JSON.stringify(window.plugin.pnav.settings);
    }
    if (localStorage['plugin-pnav-done-pokestop']) {
      pNavData.pokestop = JSON.parse(localStorage.getItem('plugin-pnav-done-pokestop'));
    }
    if (localStorage['plugin-pnav-done-gym']) {
      pNavData.gym = JSON.parse(localStorage.getItem('plugin-pnav-done-gym'));
    }
    $('#toolbox').append(`<a title="${getString('pokeNavSettingsTitle')}" onclick="if(!window.plugin.pnav.timer){window.plugin.pnav.showSettings();}return false;" accesskey="s">${getString('pokeNavSettingsText')}</a>`);
    $('body').prepend('<input id="copyInput" style="position: absolute;"></input>');
    const detailsObserver = new MutationObserver(waitForPogoButtons);
    const statusObserver = new MutationObserver(waitForPogoStatus);
    const send = Boolean(window.plugin.pnav.settings.webhookUrl);
    window.addHook('portalSelected', function (data) {
      console.log(data);
      var guid = data.selectedPortalGuid;
      selectedGuid = guid;
      if (!window.plugin.pogo) {
        setTimeout(function () {
          $('#portaldetails').append(`
          <div id="PNav" style="color:#fff">
            <Label>
              <input type="radio" checked="true" name="type" value="none" id="PNavNone"/>
              ${getString('PNavNoneDescription')}
            </label>
            <Label>
              <input type="radio" name="type" value="stop" id="PNavStop"/>
              ${getString('PNavStopDescription')}
            </label>
            <Label>
              <input type="radio" name="type" value="gym" id="PNavGym"/>
              ${getString('PNavGymDescription')}
            </label>
            <Label>
              <input type="radio" name="type" value="ex" id="PNavEx"/>
              ${getString('PNavExDescription')}
            </label>
            <a style="${window.isSmartphone() ? ';padding:5px;margin-top:3px;margin-bottom:3px;border:2px outset #20A8B1' : ''}" title="${getString('PogoButtonsTitle', {send})}" onclick="window.plugin.pnav.copy();return false;" accesskey="p">
              ${getString('PogoButtonsText', {send})}
            </a>
          </div>
        `);
        }, 0);
      } else {
        // wait for the Pogo Buttons to get added
        detailsObserver.observe($('#portaldetails')[0], {'childList': true});
        // if running on mobile, also wait for the Buttons in Status bar to get added and add it there.
        if (window.isSmartphone()) {
          statusObserver.observe($('.PogoStatus')[0], {'childList': true});
        }
      }
    });
  };

  // PLUGIN END //////////////////////////////////////////////////////////

  // add the script info data to the function as a property
  setup.info = plugin_info;
  if (!window.bootPlugins) window.bootPlugins = [];
  window.bootPlugins.push(setup);
  // if IITC has already booted, immediately run the 'setup' function
  if (window.iitcLoaded && typeof setup === 'function') setup();
}

/*
 * wrapper end
 * inject code into site context
 */
var script = document.createElement('script');
var info = {};
if (typeof GM_info !== 'undefined' && GM_info && GM_info.script) {
  info.script = {
    version: GM_info.script.version,
    name: GM_info.script.name,
    description: GM_info.script.description
  };
}
script.appendChild(document.createTextNode(`(${wrapper})(${JSON.stringify(info)});`));
(document.body || document.head || document.documentElement).appendChild(script);
