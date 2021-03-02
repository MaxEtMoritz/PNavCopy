// ==UserScript==
/* globals $, GM_info, L */
// eslint-disable-next-line multiline-comment-style
// @id             pnavcopy@maxetmoritz
// @name           IITC plugin: Copy PokeNav Command
// @category       Misc
// @downloadURL    https://raw.github.com/MaxEtMoritz/PNavCopy/main/PNavCopy.user.js
// @author         MaxEtMoritz
// @version        1.7.0
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
  /* eslint-disable no-undefined */
  window.plugin.pnav.settings = {
    webhookUrl: undefined,
    name: window.PLAYER.nickname,
    radius: undefined,
    lat: undefined,
    lng: undefined,
    language: undefined,
    useBot: false
  };
  /* eslint-enable no-undefined */
  var selectedGuid = null;
  var pNavData = {
    pokestop: {},
    gym: {}
  };
  const request = new XMLHttpRequest();

  // both bots react to mentions, no need to fiddle around with prefixes!
  const pNavId = 428187007965986826n;
  const companionId = 806533005626572813n;

  var lCommBounds;
  const wait = 2000; // Discord WebHook accepts 30 Messages in 60 Seconds.
  const discordMessageLimit = 2000; // a message can be 2000 characters at max in Discord

  const strings = {
    en: {
      alertAlreadyExported: 'This location has already been exported! If you are sure this is not the case, the creation command has been copied to clipboard for you. If this happens too often, try to reset the export state in the settings.',
      alertExportRunning: 'Settings not saved because Export was running. Pause the Export and then try again!',
      alertLanguageAfterReload: 'The new language settings will be fully in effect after a page reload.',
      alertNoModifications: 'No modifications detected!',
      alertOutsideArea: 'This location is outside the specified Community Area!',
      alertProblemPogoTools: 'There was a problem reading the Pogo Tools Data File.',
      botEditDialogTitle: 'Edit export',
      btnBulkExportGymsText: 'Export all Pogo Tools Gyms',
      btnBulkExportGymsTitle: 'Grab the File where all Gyms are stored by PoGo Tools and send them one by one via WebHook. This can take much time!',
      btnBulkExportStopsText: 'Export all Pogo Tools Stops',
      btnBulkExportStopsTitle: 'Grab the File where all Stops are stored by PoGo Tools and send them one by one via WebHook. This can take much time!',
      btnBulkModifyText: 'Check for Modifications',
      btnBulkModifyTitle: 'Check if the Pogo Tools Data was modified and start Upload process of modifications',
      btnEraseHistoryTextDefault: 'Delete Location Export History',
      btnEraseHistoryTextSuccess: 'Deleted!',
      btnEraseHistoryTitle: 'Delete all collected Export History.',
      btnExportText: 'Export Data',
      btnExportTitle: 'This will open a dialog where you can copy the PokeNav data to Clipboard.',
      btnImportText: 'Import Data',
      btnImportTitle: 'Import the exported data',
      btnSkipText: 'Skip one',
      bulkExportProgressButtonText: 'Pause',
      bulkExportProgressButtonTitle: 'Store Progress locally and stop Exporting. If you wish to restart, go to Settings and click the Export Button again.',
      bulkExportProgressTitle: 'PokeNav Bulk Export Progress',
      exportDialogTitle: 'Export',
      exportProgressBarDescription: 'Progress:',
      exportStateTextExporting: 'Exporting...',
      exportStateTextReady: 'Export Ready!',
      exportTextFieldDescription: 'Please copy this text and save it in a text file!',
      exportTimeRemainingDescription: 'Time remaining: ',
      importDialogButtonText: ['#importDialogTitle'],
      importDialogButtonTitle: 'Importing will override whatever data you currently have!',
      importDialogTitle: 'Import',
      importInputText: 'Paste the data you exported in this text field!',
      importInvalidFormat: 'The text you pasted has an invalid format! Make sure that it is the right text and that it is complete!',
      lblErrorCnText: 'Invalid Coordinate Format! Please input them like 00.0...00, 00.0...00!',
      lblErrorRdText: 'Invalid Radius! Please check if it is a valid Number!',
      lblErrorWHText: 'Invalid URL! Please delete or correct it!',
      lCommBoundsName: 'PokeNav Community',
      Modification: 'Modification ',
      of: ' of ',
      pnavCenterDescription: 'Community Center:',
      pnavCenterTitle: `Paste the Center Coordinate of your Community here (you can view it typing @PokeNav show settings in Admin Channel)`,
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
      pokeNavSettingsTitle: 'Configure PokeNav',
      portalHighlighterName: 'PokeNav State',
      useBotText: 'Use Companion Bot',
      useBotTitle: 'Tick this if you have invited the Companion Bot to your Server. This enables a faster bulk export. More Info on GitHub!'
    },
    de: {
      alertAlreadyExported: 'Dieser POI wurde schon exportiert! Wenn dies mit Sicherheit nicht der Fall ist, wurde das Kommando zum Erstellen in die Zwischenablage kopiert. Passiert dies zu häufig, versuche, den Export-Status in den Einstellungen zurückzusetzen.',
      alertExportRunning: 'Die Einstellungen wurden nicht gespeichert, da der Daten-Export läuft. Pausiere den Export und versuche es noch mal!',
      alertLanguageAfterReload: 'Die neuen Spracheinstellungen werden vollständig erst nach erneutem Laden der Seite wirksam!',
      alertNoModifications: 'Keine Änderungen gefunden!',
      alertOutsideArea: 'Dieser POI liegt nicht in den angegebenen Community-Grenzen!',
      alertProblemPogoTools: 'Es ist ein Problem beim Lesen der Pogo-Tools-Daten aufgetreten!',
      botEditDialogTitle: 'Bearbeitungs-Export',
      btnBulkExportGymsText: 'Exportiere alle Pogo Tools Arenen',
      btnBulkExportGymsTitle: 'Exportiere alle Arenen aus Pogo Tools eine nach der Anderen über den angegebenen WebHook. Dies kann eine Weile dauern!',
      btnBulkExportStopsText: 'Exportiere alle Pogo Tools Stops',
      btnBulkExportStopsTitle: 'Exportiere alle Pokestops aus Pogo Tools einer nach dem Anderen über den angegebenen WebHook. Dies kann eine Weile dauern!',
      btnBulkModifyText: 'Prüfe auf Änderungen',
      btnBulkModifyTitle: 'Prüft die Pogo-Tools-Daten auf Änderungen und beginnt den Upload-Prozess der Änderungen.',
      btnEraseHistoryTextDefault: 'Lösche Export-Historie',
      btnEraseHistoryTextSuccess: 'Gelöscht!',
      btnEraseHistoryTitle: 'Lösche die gesamte bisher gesammelte Export-Historie.',
      btnExportText: 'Exportiere Daten',
      btnExportTitle: 'Dies öffnet einen Dialog, in dem sie die PokeNav-Daten kopieren können.',
      btnImportText: 'Importiere Daten',
      btnImportTitle: 'Importiere exportierte Daten',
      btnSkipText: 'Änderung überspringen',
      bulkExportProgressButtonText: 'Pause',
      bulkExportProgressButtonTitle: 'Speichert den Fortschritt lokal und beendet den Export. Starten Sie zum Fortsetzen des Exports diesen in den Einstellungen neu.',
      bulkExportProgressTitle: 'Fortschritt des PokeNav Massen-Exports',
      exportDialogTitle: 'Export',
      exportProgressBarDescription: 'Fortschritt:',
      exportStateTextExporting: 'Exportiere...',
      exportStateTextReady: 'Export Abgeschlossen!',
      exportTextFieldDescription: 'Bitte kopieren Sie diesen Text und speichern Sie ihn in einer Textdatei!',
      exportTimeRemainingDescription: 'Verbleibende Zeit: ',
      importDialogButtonText: 'Importieren',
      importDialogButtonTitle: 'Importieren wird alle momentan gesammelten Daten überschreiben!',
      importDialogTitle: 'Import',
      importInputText: 'Fügen Sie die exportierten Daten in dieses Textfeld ein.',
      importInvalidFormat: 'Der eingefügte Text hat ein ungültiges Format. Stellen Sie sicher, dass Sie den richtigen Text vollständig eingefügt haben!',
      lblErrorCnText: 'Ungültiges Koordinaten-Format! Bitte geben Sie sie wie Folgt ein: 00.0...00, 0.0...00!',
      lblErrorRdText: 'Ungüliger Radius! Bitte überprüfen Sie, ob Sie eine gültige Zahl eingegeben haben!',
      lblErrorWHText: 'Ungültige URL! Bitte löschen oder korrigieren Sie sie!',
      Modification: 'Änderung ',
      of: ' von ',
      pnavCenterDescription: 'Community-Mittelpunkt:',
      pnavCenterTitle: `Fügen Sie die Mittelpunkt-Koordinate Ihrer Community hier ein. Sie können sie abrufen, indem Sie &quot;@PokeNav show settings&quot; in den PokeNav Administratoren-Kanal eigeben.`,
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
      pokeNavSettingsTitle: 'Konfigurieren Sie PokeNav',
      portalHighlighterName: 'PokeNav-Status',
      useBotText: 'Bot verwenden',
      useBotTitle: 'Setzen Sie den Haken, wenn Sie den Assistenz-Bot auf Ihren Server hinzugefügt haben. Dadurch kann der Massen-Export beschleunigt werden. Mehr Infos dazu auf GitHub!'
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

  function isImportInputValid (data) {
    if (Object.keys(data).length > 2 || typeof data.pokestop !== 'object' || typeof data.gym !== 'object') {
      console.error('import data has more or less top-level nodes or different ones than "pokestop" and "gym".');
      return false;
    } else {
      var validGuid = new RegExp('^[0-9|a-f]{32}\\.16$');
      var allValid = true;
      Object.keys(data.pokestop).forEach(function (guid) {
        if (allValid) {
          allValid = validGuid.test(guid);
          if (!allValid) {
            console.error(`the guid ${guid} is not a valid guid!`);
          }
          var entry = data.pokestop[guid];
          if (Object.keys(entry).length != 4 || !entry.guid || entry.guid != guid || typeof entry.lat !== 'string' || typeof entry.lng !== 'string' || typeof entry.name !== 'string') {
            allValid = false;
            console.error(`the following pokestop has invalid data: ${JSON.stringify(entry)}`);
          }
        }
      });
      if (allValid) {
        Object.keys(data.gym).forEach(function (guid) {
          if (allValid) {
            allValid = validGuid.test(guid);
            if (!allValid) {
              console.error(`the guid ${guid} is not a valid guid!`);
            }
            var entry = data.gym[guid];
            if (Object.keys(entry).length == 4 && (!entry.guid || entry.guid != guid || typeof entry.lat !== 'string' || typeof entry.lng !== 'string' || typeof entry.name !== 'string')) {
              allValid = false;
              console.error(`the following gym has invalid data: ${JSON.stringify(entry)}`);
            } else if (Object.keys(entry).length == 5 && (!entry.guid || typeof entry.isEx !== 'boolean' || entry.guid != guid || typeof entry.lat !== 'string' || typeof entry.lng !== 'string' || typeof entry.name !== 'string')) {
              allValid = false;
              console.error(`the following gym has invalid data: ${JSON.stringify(entry)}`);
            } else if (Object.keys(entry).length < 4 || Object.keys(entry).length > 5) {
              allValid = false;
              console.error(`the following gym has too much or too few properties: ${JSON.stringify(entry)}`);
            }
          }
        });
      }
      return allValid;
    }
  }

  // Highlighter that will highlight Portals according to the data that was submitted to PokeNav. PokeStops in blue, Gyms in red, Ex Gyms maybe with a yellow circle, Not yet submitted portals in gray.
  window.plugin.pnav.highlight = function (data) {
    const guid = data.portal.options.guid;
    var color, fillColor;
    if (pNavData.pokestop[guid]) {
      color = '#00d8ff';
    } else if (pNavData.gym[guid]) {
      if (pNavData.gym[guid].isEx) {
        fillColor = '#eec13c';
      }
      color = '#ff0204';
    } else {
      color = '#808080';
    }
    var params = window.getMarkerStyleOptions({team: window.TEAM_NONE,
      level: 0});
    params.color = color;
    params.fillColor = fillColor;
    data.portal.setStyle(params);
  };

  window.plugin.pnav.copy = function () {
    var input = $('#copyInput');
    if (window.selectedPortal) {
      var portal = window.portals[selectedGuid];
      // escaping Backslashes and Hyphens in Portal Names
      /** @type {string} */
      var name = portal.options.data.title
        .replaceAll('"', '\\"');
      var latLng = portal.getLatLng();
      var lat = latLng.lat;
      var lng = latLng.lng;
      var opt = ' ';
      var type = 'none';
      var isEx;

      /** @type {string} */
      const prefix = `<@${pNavId}> `;
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
        typeof window.plugin.pnav.settings.lat !== 'undefined' &&
        typeof window.plugin.pnav.settings.lng !== 'undefined' &&
        typeof window.plugin.pnav.settings.radius !== 'undefined' &&
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
      // eslint-disable-next-line no-underscore-dangle
      if (window._current_highlighter === getString('portalHighlighterName')) {
        window.changePortalHighlights(getString('portalHighlighterName')); // re-validate highlighter if active
      }
    }
  };

  window.plugin.pnav.exportData = function () {
    const dialog = window.dialog({id: 'exportDialog',
      width: 'auto',
      height: 'auto',
      title: getString('exportDialogTitle'),
      html: `<label>${getString('exportTextFieldDescription')}</label><br>
            <textarea id="exportTextField" style="width:100%;height:auto;max-width:576px;min-width:100%"></textarea>`});
    $('#exportTextField', dialog).append(JSON.stringify(pNavData, null, 2));
    const field = $('#exportTextField', dialog)[0];
    field.focus();
    field.setSelectionRange(0, field.length);
    field.select();
  };

  window.plugin.pnav.importData = function () {
    const dialog = window.dialog({id: 'importDialog',
      width: 'auto',
      heigth: 'auto',
      title: getString('importDialogTitle'),
      html: `<textarea id="importInput" style="width:100%; height:auto" placeholder="${getString('importInputText')}"/>`,
      buttons: {
        OK: {
          text: getString('importDialogButtonText'),
          title: getString('importDialogButtonTitle'),
          click () {
            try {
              var data = JSON.parse($('#importInput', dialog).val());
            } catch (e) {
              alert(getString('importInvalidFormat'));
              console.error('Parsing of import JSON Data failed.');
            }
            if (isImportInputValid(data)) {
              pNavData = data;
              saveToLocalStorage();
              // re-validate the highlighter if it is active.
              // eslint-disable-next-line no-underscore-dangle
              if (window._current_highlighter === getString('portalHighlighterName')) {
                window.changePortalHighlights(getString('portalHighlighterName'));
              }
              dialog.dialog('close');
            } else {
              alert(getString('importInvalidFormat'));
            }
          }
        }
      }});
  };

  window.plugin.pnav.showSettings = function () {
    var validURL = '^https?://discord(app)?.com/api/webhooks/[0-9]*/.*';
    var html = `
        <p>
          <label>
            ${getString('pnavLanguageDescription')}
            <select id="pnavLanguage" onchange="window.plugin.pnav.settings.language = this.value;alert(window.plugin.pnav.getString('alertLanguageAfterReload'));"></select>
          </label>
        </p>
        <p id="webhook">
          <label title="${getString('pnavhookurlTitle')}">
            ${getString('pnavhookurlDescription')}
            <input type="url" style="width:100%" id="pnavhookurl" value="${typeof window.plugin.pnav.settings.webhookUrl !== 'undefined' ? window.plugin.pnav.settings.webhookUrl : ''}" pattern="${validURL}"/>
          </label>
          <br>
          <label title="${getString('useBotTitle')}">
            <input type="checkbox" id="useBot" ${window.plugin.pnav.settings.useBot ? 'checked' : ''}></input>
            ${getString('useBotText')}
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
          <input id="pnavCenter" size="17" type="text" pattern="^-?&#92;d?&#92;d(&#92;.&#92;d+)?, -?1?&#92;d?&#92;d(&#92;.&#92;d+)?$" value="${typeof window.plugin.pnav.settings.lat !== 'undefined' && typeof window.plugin.pnav.settings.lng !== 'undefined' ? `${window.plugin.pnav.settings.lat}, ${window.plugin.pnav.settings.lng}` : ''}"/>
          </label>
          <br>
          <label id="radius" title="${getString('pnavRadiusTitle')}">
          ${getString('pnavRadiusDescription')}
          <input id="pnavRadius" style="width:41px;appearance:textfield;-moz-appearance:textfield;-webkit-appearance:textfield" type="number" min="0" step="0.001" value="${typeof window.plugin.pnav.settings.radius !== 'undefined' ? window.plugin.pnav.settings.radius : ''}"/>
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
        <p><aside>
          <button type="Button" id="btnExport" title="${getString('btnExportTitle')}" onclick="
          window.plugin.pnav.exportData();
          $(this).css('color','green');
          $(this).css('border','1px solid green')
          $(this).text('${getString('btnExportTextSuccess')}');
          setTimeout(function () {
            if($('#btnExport').length > 0){
              $('#btnExport').css('color', '');
              $('#btnExport').css('border', '');
              $('#btnExport').text('${getString('btnExportText')}');
            }
          }, 1000);return false;" style="width:49%">${getString('btnExportText')}</button>
          <button type="Button" id="btnImport" title="${getString('btnImportTitle')}" onclick="window.plugin.pnav.importData(); return false;" style="width:49%">${getString('btnImportText')}</button></aside>
        </p>
        `;
    if (window.plugin.pogo && window.plugin.pnav.settings.webhookUrl) {
      html += `
            <p><button type="Button" id="btnBulkExportGyms" style="width:100%" title="${getString('btnBulkExportGymsTitle')}" onclick="window.plugin.pnav.bulkExport('gym');return false;">${getString('btnBulkExportGymsText')}</button></p>
            <p><button type="Button" id="btnBulkExportStops" style="width:100%" title="${getString('btnBulkExportStopsTitle')}" onclick="window.plugin.pnav.bulkExport('pokestop');return false;">${getString('btnBulkExportStopsText')}</button></p>
            `;
    }
    if (window.plugin.pogo) {
      html += `
      <p><button type="Button" id="btnBulkModify" style="width:100%" title="${getString('btnBulkModifyTitle')}" onclick="window.plugin.pnav.bulkModify(); return false;">
      ${getString('btnBulkModifyText')}</button></p>
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
          var settings = {...window.plugin.pnav.settings};
          if (
            !$('#pnavhookurl').val() ||
            new RegExp(validURL).test($('#pnavhookurl').val())
          ) {
            settings.webhookUrl = $('#pnavhookurl').val();
            if ($('#lblErrorWH').length > 0) {
              $('#lblErrorWH').remove();
            }
          } else {
            if ($('#lblErrorWH').length == 0) {
              $('#webhook').after(`<label id="lblErrorWH" style="color:red">${getString('lblErrorWHText')}</label>`);
            }
            allOK = false;
          }
          if (!$('#pnavRadius').val()) {
            delete settings.radius;
            if ($('#lblErrorRd').length > 0) {
              $('#lblErrorRd').remove();
            }
          } else if (
            new RegExp('^\\d+(\\.\\d+)?$').test($('#pnavRadius').val()) &&
            !Number.isNaN(parseFloat($('#pnavRadius').val()))
          ) {
            settings.radius = parseFloat($('#pnavRadius').val());
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
            delete settings.lat;
            delete settings.lng;
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
              settings.lat = lat;
              settings.lng = lng;
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
            settings.name = window.PLAYER.nickname;
          } else {
            settings.name = $('#pnavCodename').val();
          }
          settings.useBot = $('#useBot').prop('checked');
          if (!window.plugin.pnav.timer) {
            if (allOK) {
              localStorage.setItem(
                'plugin-pnav-settings',
                JSON.stringify(settings)
              );
              window.plugin.pnav.settings = settings;
              lCommBounds.clearLayers();
              if (settings.lat && settings.lng && settings.radius) {
                var circle = L.circle(L.latLng([
                  settings.lat,
                  settings.lng
                ]), {radius: settings.radius * 1000,
                  interactive: false,
                  fillOpacity: 0.1,
                  color: '#000000'});
                lCommBounds.addLayer(circle);
              }
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
    // re-validate the highlighter if it is active.
    // eslint-disable-next-line no-underscore-dangle
    if (window._current_highlighter === getString('portalHighlighterName')) {
      window.changePortalHighlights(getString('portalHighlighterName'));
    }
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
    if (window.plugin.pnav.settings.useBot && window.plugin.pnav.settings.webhookUrl) {
      botEdit(changeList);
      return;
    }
    if (changeList && changeList.length > 0) {
      // console.log(changeList);
      const send = Boolean(window.plugin.pnav.settings.webhookUrl);
      const html = `
        <label>${getString('Modification')}</label><label id=pNavModNrCur>1</label><label>${getString('of')}</label><label id="pNavModNrMax"></label>
        <h3>
          ${getString('pNavOldPoiNameDescription')}
        </h3>
        <h3 id="pNavOldPoiName"></h3>
        <label>${getString('pNavChangesMadeDescription')}</label>
        <ul id="pNavChangesMade"></ul>
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
          sendMessage(`<@${pNavId}> ${poi.oldType}-info ${poi.oldName}`);
        } else {
          const input = $('#copyInput');
          input.show();
          input.val(`<@${pNavId}> ${poi.oldType}-info ${poi.oldName}`);
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
          updateDone([poi.guid]);
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

  /**
   * updates the export state after an edit step.
   * @param {number[]} guidList - list of guids that were edited.
   */
  function updateDone (guidList) {
    const pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : {};
    const pogoGyms = pogoData.gyms ? pogoData.gyms : {};
    const pogoStops = pogoData.stops ? pogoData.stops : {};
    guidList.forEach((guid) => {
      if (Object.keys(pogoStops).includes(guid)) {
        pNavData.pokestop[guid] = pogoStops[guid];
        if (Object.keys(pNavData.gym).includes(guid)) {
          delete pNavData.gym[guid];
        }
      } else if (Object.keys(pogoGyms).includes(guid)) {
        pNavData.gym[guid] = pogoGyms[guid];
        if (Object.keys(pNavData.pokestop).includes(guid)) {
          delete pNavData.pokestop[guid];
        }
      } else {
        if (Object.keys(pNavData.pokestop).includes(guid)) {
          delete pNavData.pokestop[guid];
        } else {
          delete pNavData.gym[guid];
        }
      }
    });
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

  function sendModCommand (changes) {
    let command = '';
    if (changes.type && changes.type === 'none') {
      command = `<@${pNavId}> delete poi ${pNavId}`;
    } else {
      command = `<@${pNavId}> update poi ${pNavId}`;
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
   * Edit Data that lists what edits should be made.
   * @typedef {object} editData
   * @property {string} oldType
   * @property {string} oldName
   * @property {string} guid
   * @property {string} [latitude]
   * @property {string} [longitude]
   * @property {string} [name]
   * @property {string} [type]
   * @property {number} [ex_eligible]
   */

  /**
   * data about portals
   * @typedef {object} portalData
   * @property {string} type
   * @property {string} guid
   * @property {string} name
   * @property {string} lat
   * @property {string} lng
   * @property {boolean} [isEx]
   */

  /**
   * Checks if a single Poi has been modified
   * @param {portalData} currentData
   * @return {editData | null} returns the found changes or null if none were found or a problem occurred.
   */
  function checkForSingleModification (currentData) {

    /** @type {editData} */
    let changes = {};

    /** @type {portalData} */
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
   * @return {portalData[] | null} returns the data to export or null if Pogo Tools Data was not found.
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
        typeof window.plugin.pnav.settings.lat === 'undefined' ||
        typeof window.plugin.pnav.settings.lng === 'undefined' ||
        typeof window.plugin.pnav.settings.radius === 'undefined';

      /** @type {portalData[]} */
      var exportData = pogoData.filter(function (/** @type{portalData}*/object) {
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
      window.onbeforeunload = function () {
        saveState(data, type, i);
        return null;
      };

      var whatToDo = (window.plugin.pnav.settings.useBot ? botExport : normalExport);

      window.plugin.pnav.timer = setInterval(() => {
        if (i < data.length && data.length > 0) {
          whatToDo(data, type, thisDialog, i).then((result) => {
            i = result;
          });
        } else {
          saveState(data, type, i);
          clearInterval(window.plugin.pnav.timer);
          window.plugin.pnav.timer = null;
          window.onbeforeunload = null;
        }
      }, wait);

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
              <label id="exportTimeRemaining">???</label>
              <label>s</label>
        `,
        width: 'auto',
        title: getString('bulkExportProgressTitle'),
        buttons: {
          OK: {
            text: getString('bulkExportProgressButtonText'),
            title: getString('bulkExportProgressButtonTitle'),
            click () {
              saveState(data, type, i);
              clearInterval(window.plugin.pnav.timer);
              window.plugin.pnav.timer = null;
              // eslint-disable-next-line no-underscore-dangle
              if (window._current_highlighter === getString('portalHighlighterName')) { // re-validate highlighter if it is enabled.
                window.changePortalHighlights(getString('portalHighlighterName'));
              }
              dialog.dialog('close');
            }
          }
        }
      });
      let thisDialog = dialog.parent();

      $('.ui-button.ui-dialog-titlebar-button-close', thisDialog).on(
        'click',
        function () {
          saveState(data, type, i);
          clearInterval(window.plugin.pnav.timer);
          window.plugin.pnav.timer = null;
        }
      );
      if (data.length > 0) {
        whatToDo(data, type, dialog, 0).then((result) => {
          i = result;
        }); // start immediately instead of waiting 2 seconds.
      } else {
        updateExportDialog(thisDialog, 0, 0, 0);
      }
    } else {
      console.log('Bulk Export already running!');
    }
  };

  /**
   * One Export step when the Companion Discord bot should be used.
   * @param {portalData[]} data all locations that need exporting
   * @param {string} type the location type of the given data
   * @param {HTMLElement} dialog the dialog of the bulk export
   * @param {number} i the current index
   * @return {number} the new index after the export step
   */
  async function botExport (data, type, dialog, i) {
    var content = `<@${companionId}>cm `;
    var currentSize = content.length + 2; // command plus outer array brackets
    var toExport = [];
    let j = i;
    while (j < data.length && currentSize + 10 < discordMessageLimit) {
      let entry = data[j];
      let count = 13; // "[type,"name","lat","lng",ex]," -> results in 12 extra chars for no ex, if ex it is 14 (including ex itself). type is always one char, so add it right here.
      if (entry.isEx) {
        count += 2;
      }
      if (typeof (entry.name) === 'undefined' || typeof (entry.lat) === 'undefined' || typeof (entry.lng) === 'undefined') {
        j++; // yes, in my testings i came across a PokeStop that somehow had no name in Pogo Tools... but that makes no sense to export because PokeNav would refuse this anyway.
        break;
      }
      count += entry.lat.toString().length; // most of the time, lat and lng were strings in Pogo Tools, but on some PoIs it were numbers
      count += entry.lng.toString().length; // and .length of a number is undefined, turning count to NaN when adding it.
      count += entry.name.toString().length; // even with the name it is like that! sometimes it is just a number (had a PokeStop named 1895)!
      if (currentSize + count + 10 < discordMessageLimit) {
        currentSize += count;
        let current = [
          type === 'pokestop' ? 0 : 1,
          entry.name.toString(),
          entry.lat.toString(),
          entry.lng.toString()
        ];
        if (entry.isEx) {
          current.push(1);
        }
        toExport.push(current);
        j++;
      } else {
        break;
      }
    }
    content += JSON.stringify(toExport);
    const params = {
      username: window.plugin.pnav.settings.name,
      avatar_url: '',
      content
    };
    var success = await fetch(window.plugin.pnav.settings.webhookUrl, {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify(params)
    })
      .then((response) => {
        if (!response.ok) {
          console.error(`HTTP Error: ${response.status} - ${response.statusText}${response.bodyUsed ? `; body: ${response.body}` : ''}`);
        }
        return response.ok;
      })
      .catch((error) => {
        console.error(error);
        return false;
      });
    if (success) {
      updateExportDialog(dialog, j, Object.keys(data).length, Math.ceil(((Object.keys(data).length - j) / 40) * (wait / 1000))); // 40 locations per message is my experience from testing.
      return j; // return the new i!
    } else {
      return i;
    }
  }

  /**
   * One Export step when only the WebHook should be used.
   * @param {object} data all locations that need exporting
   * @param {string} type the location type of the given data
   * @param {HTMLElement} dialog the dialog of the bulk export
   * @param {number} i the current index
   * @return {number} the new index after the export step (normally old index + 1).
   */
  async function normalExport (data, type, dialog, i) {
    if (i % 10 == 0) {
      saveState(data, type, i); // sometimes save the state in case someone exits IITC Mobile without using the Back Button
    }
    var entry = data[i];
    let lat = entry.lat;
    let lng = entry.lng;
    // escaping Hyphens in Portal Names
    let name = entry.name
      .replaceAll('"', '\\"');
    let prefix = `<@${pNavId}> `;
    let ex = Boolean(entry.isEx);
    let options = ex ? ' "ex_eligible: 1"' : '';
    var content = `${prefix}create poi ${type} "${name}" ${lat} ${lng}${options}`;
    const params = {
      username: window.plugin.pnav.settings.name,
      avatar_url: '',
      content
    };
    var success = await fetch(window.plugin.pnav.settings.webhookUrl, {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify(params)
    })
      .then((response) => {
        if (!response.ok) {
          console.error(`HTTP Error: ${response.status} - ${response.statusText}${response.bodyUsed ? `; body: ${response.body}` : ''}`);
        }
        return response.ok;
      })
      .catch((error) => {
        console.error(error);
        return false;
      });
    if (success) {
      updateExportDialog(dialog, i + 1, Object.keys(data).length, (Object.keys(data).length - (i + 1)) * (wait / 1000));
      return i + 1; // return the new i (old i + 1)!
    } else {
      return i;
    }
  }

  /**
   * assembles the edit data in a compact format for the companion bot and send it.
   * @param {editData[]} [changes] - optional list of changes that should be transferred.
   */
  function botEdit (changes) { // uncaught TypeError: e is null! don't know which e, why it is null and what its value should be... happens after exiting the function!
    if (!changes || !(changes instanceof Array)) {
      // eslint-disable-next-line no-param-reassign
      changes = checkForModifications();
    }
    if (window.plugin.pnav.timer) {
      console.log('Bulk Export already running!');
      return;
    }
    var j = 0;

    function work () {
      $('#editProgressBar', dlg).val(j);
      $('#editNumber', dlg).text(j); // TODO finish updating the dialog, testing everything!
      if (j >= changes.length) {
        clearInterval(window.plugin.pnav.timer);
        window.plugin.pnav.timer = null;
        $('#editState', dlg).text(getString('exportStateTextReady'));
        $('#editOKButton', dlg.parent).text('OK');
        $('#editOKButton', dlg.parent).prop('title', '');
        return;
      }
      var data = [];
      var count = 2;
      var i = j;
      while (i < changes.length && count < wait - 10) {
        var current = changes[i];
        var e = {};
        if (current.type) {
          switch (current.type) {
          case 'stop':
            e.t = 0;
            break;
          case 'gym':
            e.t = 1;
            break;
          case 'none':
            e.t = 2;
            break;
          default:
            break;
          }
          count += 6; // "t":0,
        }
        if (current.latitude) {
          e.a = current.latitude;
          count += 7 + current.latitude.toString().length; // "a":"",
        }
        if (current.longitude) {
          e.o = current.longitude;
          count += 7 + current.latitude.toString().length; // "o":"",
        }
        if (current.name) {
          e.n = current.name;
          count += 7 + current.name.toString().length; // "n":"",
        }
        if (current.ex_eligible) {
          e.e = current.ex_eligible;
          count += 6; // "e":1,
        }
        data.push({n: current.oldName,
          t: current.oldType === 'stop' ? 0 : 1,
          e});
        count += 22 + current.oldName.toString().length; // {"n":"","t":0,"e":{}}, => 22 chars including type.
        i++;
      }
      $.ajax({
        method: 'POST',
        url: window.plugin.pnav.settings.webhookUrl,
        contentType: 'application/json',
        context: this,
        processData: false,
        data: JSON.stringify({
          username: window.plugin.pnav.settings.name,
          avatar_url: '',
          content: `<@${companionId}> e ${JSON.stringify(data)}`
        }),
        success () {
          var guidList = [];
          let subarray = changes.slice(j, i);
          subarray.forEach((data) => {
            guidList.push(data.guid);
          });
          updateDone(guidList);
          j = i;
          return true;
        },
        error (jgXHR, textStatus, errorThrown) {
          console.error(`${textStatus} - ${errorThrown}`);
        }
      });
    }

    var dlg = window.dialog({id: 'botEditDialog',
      width: 'auto',
      height: 'auto',
      title: getString('botEditDialogTitle'),
      html: `
        <h3 id="editState">${getString('exportStateTextExporting')}</h3>
        <p>
         <label>
            ${getString('exportProgressBarDescription')}
            <progress id="editProgressBar" value="0" max="${changes.length}"/>
          </label>
        </p>
        <label id="editNumber">0</label>
        <label>${getString('of')} ${changes.length}</label>
      `,
      buttons: {
        OK: {
          click () {
            // eslint-disable-next-line no-underscore-dangle
            if (window._current_highlighter === getString('portalHighlighterName')) {
              window.changePortalHighlights(getString('portalHighlighterName')); // re-validate highlighter if active
            }
            clearInterval(window.plugin.pnav.timer);
            dlg.dialog('close');
          },
          text: getString('bulkExportProgressButtonText'),
          title: getString('bulkExportProgressButtonTitle'),
          id: 'editOKButton'
        }
      }});

    work(); // start immediately, not wait 2s for the first message!
    if (j < changes.length) {
      window.plugin.pnav.timer = setInterval(work, wait);
    }
  }

  /**
   * updates the bulk export dialog (called by the specific export functions).
   * @param {HTMLElement} dialog the export dialog
   * @param {number} cur current export state
   * @param {number} max count of all elements that need exporting
   * @param {number} time remaining time
   */
  function updateExportDialog (dialog, cur, max, time) {
    console.log(dialog);
    if ($('#exportProgressBar', dialog)) {
      $('#exportProgressBar', dialog).val(cur);
    }
    if ($('#exportNumber', dialog)) {
      $('#exportNumber', dialog).text(cur);
    }
    if ($('#exportTimeRemaining', dialog)) {
      $('#exportTimeRemaining', dialog).text(time);
    }
    if (cur >= max) {
      $('#exportState', dialog).text(getString('exportStateTextReady'));
      const okayButton = $('.ui-button', dialog.parentElement).not('.ui-dialog-titlebar-button');
      okayButton.text('OK');
      okayButton.prop('title', '');
    }
  }

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

  function sendMessageFetch (message) {
    const params = {
      username: window.plugin.pnav.settings.name,
      avatar_url: '',
      content: message
    };
    fetch(window.plugin.pnav.settings.webhookUrl, {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: params
    })
      .then((response) => {
        if (response.ok) {
          return true;
        } else {
          console.error(`HTTP Error: ${response.status} - ${response.statusText}${response.bodyUsed ? `; body: ${response.body}` : ''}`);
          return false;
        }
      })
      .catch((error) => {
        console.error(error);
      });
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
    lCommBounds = new L.LayerGroup();
    if (window.plugin.pnav.settings.lat && window.plugin.pnav.settings.lng && window.plugin.pnav.settings.radius) {
      var commCircle = L.circle(L.latLng([
        window.plugin.pnav.settings.lat,
        window.plugin.pnav.settings.lng
      ]), {radius: window.plugin.pnav.settings.radius * 1000,
        interactive: false,
        fillOpacity: 0.1,
        color: '#000000'});
      lCommBounds.addLayer(commCircle);
    }
    window.addLayerGroup(getString('lCommBoundsName'), lCommBounds);
    window.addPortalHighlighter(getString('portalHighlighterName'), window.plugin.pnav.highlight);
    var isLinksDisplayed = window.isLayerGroupDisplayed('Links', false);
    var isFieldsDisplayed = window.isLayerGroupDisplayed('Fields', false);
    $('#portal_highlight_select').on('change', function () {
      // eslint-disable-next-line no-underscore-dangle
      if (window._current_highlighter === getString('portalHighlighterName')) {
        isLinksDisplayed = window.isLayerGroupDisplayed('Links', false);
        isFieldsDisplayed = window.isLayerGroupDisplayed('Fields', false);
        // eslint-disable-next-line no-underscore-dangle
        const layers = window.layerChooser._layers;
        const layerIds = Object.keys(layers);
        layerIds.forEach(function (id) {
          const layer = layers[id];
          if (layer.name === 'Links' || layer.name === 'Fields') {
            window.map.removeLayer(layer.layer);
          } else if ((new RegExp('Level . Portals').test(layer.name) ||
                      layer.name === 'Resistance' ||
                      layer.name === 'Enlightened' ||
                      layer.name === 'Unclaimed/Placeholder Portals') &&
                      !window.isLayerGroupDisplayed(layer.name, false)) {
            window.map.addLayer(layer.layer);
          }
        });
      } else if (!window.isLayerGroupDisplayed('Links', false) && !window.isLayerGroupDisplayed('Fields', false)) {
        // eslint-disable-next-line no-underscore-dangle
        const layers = window.layerChooser._layers;
        const layerIds = Object.keys(layers);
        layerIds.forEach(function (id) {
          const layer = layers[id];
          if ((layer.name === 'Links' && isLinksDisplayed) || (layer.name === 'Fields' && isFieldsDisplayed)) {
            window.map.addLayer(layer.layer);
          }
        });
        isLinksDisplayed = false;
        isFieldsDisplayed = false;
      }
    });

    window.addHook('portalSelected', function (data) {
      console.log(data);
      var guid = data.selectedPortalGuid;
      selectedGuid = guid;
      if (!window.plugin.pogo) {
        setTimeout(function () {
          $('#portaldetails').append(`
          <div id="PNav" style="color:#fff">
            <Label>
              <input type="radio" checked name="type" value="none" id="PNavNone"/>
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
          if (pNavData.pokestop[selectedGuid]) {
            $('#PNavStop').prop('checked', true);
          } else if (pNavData.gym[selectedGuid]) {
            if (pNavData.gym[selectedGuid].isEx) {
              $('#PNavEx').prop('checked', true);
            } else {
              $('#PNavGym').prop('checked', true);
            }
          }
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
