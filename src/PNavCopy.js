/* globals $, L, SCRIPT_INFO */

import {checkDistance, copyfieldvalue, sendMessage} from './utils';
import {detectLanguage, getString, supportedLanguages} from './l10n';
import bulkExportDialog from './templates/bulkExport.hbs';
import bulkModifyDialog from './templates/bulkModify.hbs';
import portalDetails from './templates/portalDetailsStandalone.hbs';
import settingsDialog from './templates/settings.hbs';

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

// use own namespace for plugin
window.plugin.pnav = function () {};

// Language is set in setup() if not already present in localStorage.
/* eslint-disable no-undefined */
let settings = {
  webhookUrl: null,
  name: window.PLAYER ? window.PLAYER.nickname : '', // if not yet logged in to Intel, window.PLAYER is undefined.
  radius: null,
  lat: null,
  lng: null,
  language: undefined,
  useBot: false
};
/* eslint-enable no-undefined */
let selectedGuid = null;
let pNavData = {
  pokestop: {},
  gym: {}
};

let timer = null;

// both bots react to mentions, no need to fiddle around with prefixes!
const pNavId = 428187007965986826n;
const companionId = 806533005626572813n;

/** @type {L.LayerGroup} */
let lCommBounds;
const wait = 2000; // Discord WebHook accepts 30 Messages in 60 Seconds.

function isImportInputValid (data) {
  if (Object.keys(data).length > 2 || typeof data.pokestop !== 'object' || typeof data.gym !== 'object') {
    console.error('import data has more or less top-level nodes or different ones than "pokestop" and "gym".');
    return false;
  } else {
    let validGuid = /^[0-9|a-f]{32}\.1[126]$/; // TODO: What are valid Guid endings? seen .11, .12 and .16 so far. but are there more and what are the rules?
    let allValid = true;
    Object.keys(data.pokestop).forEach(function (guid) {
      if (allValid) {
        allValid = validGuid.test(guid);
        if (!allValid) {
          console.error(`the guid ${guid} is not a valid guid!`);
        }
        let entry = data.pokestop[guid];
        if (Object.keys(entry).length < 4 || !entry.guid || entry.guid != guid || typeof entry.lat === 'undefined' || typeof entry.lng === 'undefined' || typeof entry.name === 'undefined') {
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
          let entry = data.gym[guid];
          if (
            Object.keys(entry).length < 4 ||
            !entry.guid ||
            entry.guid != guid ||
            typeof entry.lat === 'undefined' ||
            typeof entry.lng === 'undefined' ||
            typeof entry.name === 'undefined' ||
            (entry.isEx && typeof entry.isEx !== 'boolean')
          ) {
            allValid = false;
            console.error(`the following gym has invalid data: ${JSON.stringify(entry)}`);
          }
        }
      });
    }
    return allValid;
  }
}

// Highlighter that will highlight Portals according to the data that was submitted to PokeNav. PokeStops in blue, Gyms in red, Ex Gyms maybe with a yellow circle, Not yet submitted portals in gray.
function highlight (data) {
  const guid = data.portal.options.guid;
  let color, fillColor;
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
  let params = window.getMarkerStyleOptions({team: window.TEAM_NONE,
    level: 0});
  params.color = color;
  params.fillColor = fillColor;
  data.portal.setStyle(params);
}

function copy () {
  let input = $('#copyInput');
  if (window.selectedPortal) {
    let portal = window.portals[selectedGuid];

    const prefix = `<@${pNavId}> `;

    /** @type {portalData} */
    let data;

    if (window.plugin.pogo && localStorage['plugin-pogo']) {
      let toolData = JSON.parse(localStorage['plugin-pogo']);
      if (toolData.pokestops && toolData.pokestops[selectedGuid]) {
        data = toolData.pokestops[selectedGuid];
        data.type = 'pokestop';
      } else if (toolData.gyms && toolData.gyms[selectedGuid]) {
        data = toolData.gyms[selectedGuid];
        data.type = 'gym';
      } else {
        data = {
          name: portal.options.data.title,
          lat: portal.getLatLng().lat,
          lng: portal.getLatLng().lng,
          guid: portal.options.guid,
          type: 'none'
        };
      }
    } else {
      data = {
        name: portal.options.data.title,
        lat: portal.getLatLng().lat,
        lng: portal.getLatLng().lng,
        guid: portal.options.guid
      };

      switch ($('input[name=type]:checked').val()) {
      case 'pokestop':
        data.type = 'pokestop';
        break;
      case 'gym':
        data.type = 'gym';
        break;
      case 'ex':
        data.type = 'gym';
        data.isEx = true;
        break;
      case 'none':
        data.type = 'none';
        break;
      default:
        break;
      }
    }

    if (
      settings.lat !== null &&
      settings.lng !== null &&
      settings.radius !== null &&
      checkDistance(data.lat, data.lng, settings.lat, settings.lng) > settings.radius
    ) {
      alert(getString('alertOutsideArea', settings.language));
    } else {
      let changes = checkForSingleModification(data);
      if (changes) {
        bulkModify([changes]);
      } else if (data.type !== 'none') {
        if (pNavData[data.type][selectedGuid]) {
          alert(getString('alertAlreadyExported', settings.language));
          input.show();
          input.val(`${prefix}create poi ${data.type} «${data.name}» ${data.lat} ${data.lng}${data.isEx ? ' "ex_eligible: 1"' : ''}`);
          copyfieldvalue('copyInput');
          input.hide();
        } else if (settings.webhookUrl) {
          if (settings.useBot) {
            let formData = new FormData();
            formData.append('content', `<@${companionId}> cm`);
            formData.append('username', settings.name);
            formData.append('file', new Blob([JSON.stringify(data)], {type: 'application/json'}), `creation.json`);
            $.ajax({
              method: 'POST',
              url: settings.webhookUrl,
              contentType: 'application/json',
              processData: false,
              data: formData,
              error (jgXHR, textStatus, errorThrown) {
                console.error(`${textStatus} - ${errorThrown}`);
              }
            });
          } else {
            sendMessage(`${prefix}create poi ${data.type} «${data.name}» ${data.lat} ${data.lng}${data.isEx ? ' "ex_eligible: 1"' : ''}`);
          }
        } else {
          input.show();
          input.val(`${prefix}create poi ${data.type} «${data.name}» ${data.lat} ${data.lng}${data.isEx ? ' "ex_eligible: 1"' : ''}`);
          copyfieldvalue('copyInput');
          input.hide();
        }
        pNavData[data.type][selectedGuid] = data;
        saveToLocalStorage();
      }
    }
    // eslint-disable-next-line no-underscore-dangle
    if (window._current_highlighter === getString('portalHighlighterName', settings.language)) {
      window.changePortalHighlights(getString('portalHighlighterName', settings.language)); // re-validate highlighter if active
    }
  }
}

function imExport () {

  /*
   * Methods to upload / download files inspired by PoGoTools Plugin by AlfonsoML on GitLab:
   * https://gitlab.com/AlfonsoML/pogo-s2/-/blob/master/s2check.user.js
   */

  let date = new Date();
  let html = `<button type="Button" id="exportBtn" title="${getString('btnExportTitle', settings.language)}">${getString('btnExportText', settings.language)}</button>
    <hr>`;
  if (typeof L.FileListLoader !== 'undefined' || typeof window.requestFile !== 'undefined') {
    html += `<button id="importBtn" type="Button" title="${getString('btnImportTitle', settings.language)}">${getString('btnImportText', settings.language)}</button>`;
  } else if (File && FileReader && Blob) {
    html += `<form id="importForm">
      <input type="file" id="importFile" name="import" accept="application/json"><br>
      <input type="submit" value="${getString('btnImportText', settings.language)}" title="${getString('btnImportTitle', settings.language)}" class="Button">
      </form>`;
  } else {
    html += `<textarea id="importInput" style="width:100%; height:auto" placeholder="${getString('importInputText', settings.language)}"></textarea>
      <button type="Button" class="Button" id="importDialogButton" title="${getString('btnImportTitle', settings.language)}">${getString('btnImportText', settings.language)}</button>`;
  }
  const dialog = window.dialog({id: 'imExportDialog',
    width: 'auto',
    height: 'auto',
    title: getString('imExportDialogTitle', settings.language),
    html});

  $('#exportBtn').on('click', () => {
    if (typeof window.saveFile !== 'undefined') {
      window.saveFile(JSON.stringify(pNavData, null, 2), `IITCPokenavExport-${settings.name}-${date.getFullYear()}-${date.getMonth()}-${date.getDate()}.json`, 'application/json');
    } else if (typeof window.android !== 'undefined' && window.android.saveFile) {
      window.android.saveFile(`IITCPokenavExport-${settings.name}-${date.getFullYear()}-${date.getMonth()}-${date.getDate()}.json`, 'application/json', JSON.stringify(pNavData));
    } else {
      // Idea taken from https://stackoverflow.com/a/50230647 by User KeshavDulal
      const tmpTextarea = document.createElement('textarea');
      tmpTextarea.innerHTML = JSON.stringify(pNavData, null, 2);
      document.body.appendChild(tmpTextarea);
      tmpTextarea.select();
      document.execCommand('copy');
      document.body.removeChild(tmpTextarea);
      alert(getString('alertExportCopied', settings.language));
    }
  });
  if ($('#importBtn').length > 0) {
    $('#importBtn').on('click', () => {
      if (typeof L.FileListLoader !== 'undefined') {
        L.FileListLoader.loadFiles({accept: 'text/json'}).on('load', (e) => {
          // application/json did somehow not work for me on my smartphone...
          let data = JSON.parse(e.reader.result);
          if (isImportInputValid(data)) {
            pNavData = data;
            saveToLocalStorage();
            dialog.dialog('close');
            // re-validate the highlighter if it is active.
            // eslint-disable-next-line no-underscore-dangle
            if (window._current_highlighter === getString('portalHighlighterName', settings.language)) {
              window.changePortalHighlights(getString('portalHighlighterName', settings.language));
            }
          } else {
            alert(getString('importInvalidFormat', settings.language));
          }
        });
      } else if (typeof window.requestFile !== 'undefined') {
        window.requestFile((name, content) => {
          let data = JSON.parse(content);
          if (isImportInputValid(data)) {
            pNavData = data;
            saveToLocalStorage();
            dialog.dialog('close');
            // re-validate the highlighter if it is active.
            // eslint-disable-next-line no-underscore-dangle
            if (window._current_highlighter === getString('portalHighlighterName', settings.language)) {
              window.changePortalHighlights(getString('portalHighlighterName', settings.language));
            }
          } else {
            alert(getString('importInvalidFormat', settings.language));
          }
        });
      }
    });
  } else if (File && FileReader && Blob) {
    $('#importForm', dialog).on('submit', function (e) {
      e.preventDefault();
      console.debug('form submitted!');
      if ($('#importFile', dialog).prop('files').length == 1) {
        let fr = new FileReader();
        fr.onload = function () {
          console.debug('file text loaded!');
          const data = JSON.parse(fr.result);
          if (isImportInputValid(data)) {
            pNavData = data;
            saveToLocalStorage();
            dialog.dialog('close');
            // re-validate the highlighter if it is active.
            // eslint-disable-next-line no-underscore-dangle
            if (window._current_highlighter === getString('portalHighlighterName', settings.language)) {
              window.changePortalHighlights(getString('portalHighlighterName', settings.language));
            }
          } else {
            alert(getString('importInvalidFormat', settings.language));
          }
        };
        fr.readAsText($('#importFile', dialog).prop('files')[0]);
      }
    });
  } else {
    $('#importDialogButton', dialog).on('click', function () {
      let data;
      try {
        data = JSON.parse($('#importInput', dialog).val());
      } catch (e) {
        alert(getString('importInvalidFormat', settings.language));
        console.error(`Parsing of import JSON Data failed: ${e.message}`);
        return;
      }
      if (isImportInputValid(data)) {
        pNavData = data;
        saveToLocalStorage();
        // re-validate the highlighter if it is active.
        // eslint-disable-next-line no-underscore-dangle
        if (window._current_highlighter === getString('portalHighlighterName', settings.language)) {
          window.changePortalHighlights(getString('portalHighlighterName', settings.language));
        }
        dialog.dialog('close');
      } else {
        alert(getString('importInvalidFormat', settings.language));
      }
    });
  }
}

function showSettings () {
  const container = window.dialog({
    id: 'pnavsettings',
    width: 'auto',
    height: 'auto',
    html: settingsDialog({
      language: settings.language,
      settings,
      pogoToolsAvailable: Boolean(window.plugin.pogo)
    }),
    title: getString('pnavsettingsTitle', settings.language),
    buttons: {
      OK () {
        if (!timer) {
          lCommBounds.clearLayers();
          if (settings.lat && settings.lng && settings.radius) {
            let circle = L.circle(L.latLng([
              settings.lat,
              settings.lng
            ]), {
              radius: settings.radius * 1000,
              interactive: false,
              fillOpacity: 0.1,
              color: '#000000'
            });
            lCommBounds.addLayer(circle);
          }
        } else {
          alert(getString('alertExportRunning', settings.language));
        }
        container.dialog('close');
      }
    }
  });

  /** @type {JQuery<HTMLSelectElement>}*/
  let languageDropdown = $('#pnavLanguage', container);
  supportedLanguages.forEach(function (key) {
    languageDropdown.append(`<option value="${key}">${key}</option>`);
  });
  languageDropdown.val(settings.language);
  languageDropdown.on('change', (e) => {
    settings.language = e.target.value;
    alert(getString('alertLanguageAfterReload', e.target.value));
  });
  $('input', container).on('blur', validateAndSaveSetting);
  $('#btnEraseHistory', container).on('click', (e) => {
    deleteExportState();
    $(e.target).css('color', 'green');
    $(e.target).css('border', '1px solid green');
    $(e.target).text(getString('btnEraseHistoryTextSuccess', settings.language));
    setTimeout(() => {
      if ($('#btnEraseHistory').length > 0) {
        $('#btnEraseHistory').css('color', '');
        $('#btnEraseHistory').css('border', '');
        $('#btnEraseHistory').text(getString('btnEraseHistoryTextDefault', settings.language));
      }
    }, 1000);
  });
  $('#btnImExport', container).on('click', () => imExport());
  $('#btnBulkExportGyms', container).on('click', () => bulkExport('gym'));
  $('#btnBulkExportStops', container).on('click', () => bulkExport('pokestop'));
  $('#btnBulkModify', container).on('click', () => bulkModify());
}

/**
 * Validates an input field and saves the corresponding setting if valid.
 * @param {JQuery.BlurEvent<HTMLElement, undefined, HTMLInputElement, HTMLElement>} e - the JQuery event
 */
function validateAndSaveSetting (e) {
  console.log('blur');
  if (e.currentTarget.checkValidity()) {
    console.log('valid');
    // special handling of center input
    if (e.currentTarget.id === 'latlng') {
      let lat, lng;
      if (e.currentTarget.value) {
        let value = e.currentTarget.value?.split(',', 2);
        lat = parseFloat(value[0]);
        lng = parseFloat(value[1]);
        if (!(lat >= -90 && lat <= 90) || !(lng >= -180 && lng <= 180)) {
          if (e.currentTarget.nextElementSibling && e.currentTarget.nextElementSibling instanceof HTMLDivElement) {
            e.currentTarget.nextElementSibling.style.display = '';
          }
          return;
        }
      }
      settings.lat = lat;
      settings.lng = lng;
      if (e.currentTarget.nextElementSibling && e.currentTarget.nextElementSibling instanceof HTMLDivElement) e.currentTarget.nextElementSibling.style.display = 'none';
      localStorage.setItem('plugin-pnav-settings', JSON.stringify(settings));
    } else if (Object.getOwnPropertyNames(settings).includes(e.currentTarget.id)) {
      let value;
      switch (e.currentTarget.type) {
      case 'checkbox':
        value = e.currentTarget.checked;
        break;
      case 'number':
        value = e.currentTarget.valueAsNumber;
        break;
      default:
        value = e.currentTarget.value;
        break;
      }
      // if no value but placeholder, use placeholder
      if (!value && e.currentTarget.placeholder) {
        value = e.currentTarget.placeholder;
      }
      if (value === '') {
        value = null;
      }
      if (e.currentTarget.nextElementSibling && e.currentTarget.nextElementSibling instanceof HTMLDivElement) e.currentTarget.nextElementSibling.style.display = 'none';
      settings[e.currentTarget.id] = value;
      localStorage.setItem('plugin-pnav-settings', JSON.stringify(settings));
    }
  } else {
    console.log('invalid', e.currentTarget.nextSibling, e.currentTarget);
    // TODO: show error message
    if (e.currentTarget.nextElementSibling && e.currentTarget.nextElementSibling instanceof HTMLDivElement) {
      e.currentTarget.nextElementSibling.style.display = '';
    }
  }
}

function deleteExportState () {
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
  if (window._current_highlighter === getString('portalHighlighterName', settings.language)) {
    window.changePortalHighlights(getString('portalHighlighterName', settings.language));
  }
}

/**
 * saves the State of the Bulk Export to local Storage.
 * @param {pogoToolsData[]} data
 * @param {string} type
 * @param {number} [index]
 */
function saveState (data, type, index) {
  const addToDone = data.slice(0, index);
  addToDone.forEach(function (object) {
    pNavData[type][object.guid] = object;
  });
  saveToLocalStorage();
}

function bulkModify (changes) {
  const changeList = changes && changes instanceof Array ? changes : checkForModifications();
  if (settings.useBot && settings.webhookUrl) {
    botEdit(changeList);
    return;
  }
  if (changeList && changeList.length > 0) {
    let i = 0;
    const send = Boolean(settings.webhookUrl);
    const html = bulkModifyDialog({
      language: settings.language,
      options: {send}
    });

    /** @type{JQuery<HTMLElement>}*/
    const modDialog = window.dialog({
      id: 'pNavmodDialog',
      title: getString('pNavmodDialogTitle', settings.language),
      html,
      width: 'auto',
      height: 'auto',
      buttons: {
        Skip: {
          id: 'btnSkip',
          text: getString('btnSkipText', settings.language),
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
    let poi = changeList[i];
    $('#pNavPoiInfo', modDialog).on('click', function () {
      if (settings.webhookUrl) {
        sendMessage(`<@${pNavId}> ${poi.oldType === 'pokestop' ? 'stop' : poi.oldType}-info ${poi.oldName}`);
      } else {
        const input = $('#copyInput');
        input.show();
        input.val(`<@${pNavId}> ${poi.oldType === 'pokestop' ? 'stop' : poi.oldType}-info ${poi.oldName}`);
        copyfieldvalue('copyInput');
        input.hide();
      }
    });
    $('#pNavPoiId', modDialog).on('input', function (e) {
      const valid = e.target.validity.valid;
      const value = e.target.valueAsNumber;
      if (valid && value && value > 0) {
        $('#pNavModCommand', modDialog).prop('style', 'margin-top:5px');
        $('#pNavModCommand', modDialog).prop('title', getString('pNavModCommandTitleEnabled', settings.language, {send}));
      } else {
        $('#pNavModCommand', modDialog).css('color', 'darkgray');
        $('#pNavModCommand', modDialog).css('text-decoration', 'none');
        $('#pNavModCommand', modDialog).css('border', '1px solid darkgray');
        $('#pNavModCommand', modDialog).prop('title', getString('pNavModCommandTitleDisabled', settings.language, {send}));
      }
    });
    $('#pNavModCommand', modDialog).on('click', function () {
      if ($('#pNavPoiId', modDialog).val() && (/^\d*$/).test($('#pNavPoiId', modDialog).val())) {
        sendModCommand($('#pNavPoiId', modDialog).val(), poi);
        updateDone([poi]);
        i++;
        if (i == changeList.length) {
          modDialog.dialog('close');
        } else {
          poi = changeList[i];
          updateUI(modDialog, poi, i);
        }
      }
    });
    $('#address').on('click', () => {
      $.ajax(`https://nominatim.openstreetmap.org/reverse?lat=${changeList[i].lat}&lon=${changeList[i].lng}&format=json&addressdetails=0`, {
        success: (data) => {
          if (data && data.display_name) {
            $('#addressdetails').text(data.display_name);
            $('#address').prop('hidden', true);
            $('#addressdetails').prop('hidden', false);
          }
        }
      });
    });
    console.log(modDialog);
    modDialog.css('width', `${modDialog[0].offsetWidth}px`);
    $('#pNavModNrMax', modDialog).text(changeList.length);
    updateUI(modDialog, poi, i);
  } else {
    alert(getString('alertNoModifications', settings.language));
  }
}

/**
 * updates the export state after an edit step.
 * @param {editData[]} changeList - list of changes that were made.
 */
function updateDone (changeList) {
  const pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : {};
  const pogoGyms = pogoData.gyms ?? {};
  const pogoStops = pogoData.pokestops ?? {};
  changeList.forEach((change) => {
    if (Object.keys(pogoStops).includes(change.guid)) {
      pNavData.pokestop[change.guid] = pogoStops[change.guid];
      if (Object.keys(pNavData.gym).includes(change.guid)) {
        delete pNavData.gym[change.guid];
      }
    } else if (Object.keys(pogoGyms).includes(change.guid)) {
      pNavData.gym[change.guid] = pogoGyms[change.guid];
      if (Object.keys(pNavData.pokestop).includes(change.guid)) {
        delete pNavData.pokestop[change.guid];
      }
    } else {
      if (Object.keys(pNavData.pokestop).includes(change.guid)) {
        delete pNavData.pokestop[change.guid];
      } else {
        delete pNavData.gym[change.guid];
      }
    }
  });
  saveToLocalStorage();
}

function updateUI (dialog, poi, i) {
  $('#pNavOldPoiName', dialog).text(poi.oldName);
  $('#pNavModNrCur', dialog).text(i + 1);
  $('#pNavChangesMade', dialog).empty();
  $('#addressdetails').text('')
    .prop('hidden', true);
  $('#address').prop('hidden', false);
  for (const [
    key,
    value
  ] of Object.entries(poi.edits)) {
    $('#pNavChangesMade', dialog).append(`<li>${key} => ${value}</li>`);
  }
  $('#pNavPoiId', dialog).val('');
  $('#pNavModCommand', dialog).css('color', 'darkgray');
  $('#pNavModCommand', dialog).css('border', '1px solid darkgray');
  $('#pNavModCommand', dialog).css('cursor', 'default');
  $('#pNavModCommand', dialog).css('text-decoration', 'none');
  $('#pNavModCommand', dialog).prop('title', getString('pNavModCommandTitleDisabled', settings.language, {send: Boolean(settings.webhookUrl)}));
}

function sendModCommand (poiId, changes) {
  let command = '';
  if (changes.edits.type && changes.edits.type === 'none') {
    command = `<@${pNavId}> delete poi ${poiId}`;
  } else {
    command = `<@${pNavId}> update poi ${poiId}`;
    for (const [
      key,
      value
    ] of Object.entries(changes.edits)) {
      command += ` «${key}: ${value}»`;
    }
  }
  if (settings.webhookUrl) {
    sendMessage(command);
  } else {
    const input = $('#copyInput');
    input.show();
    input.val(command);
    copyfieldvalue('copyInput');
    input.hide();
  }
}

/**
 * Checks for Modifications in PoGoTools Data.
 * @return {editData[]} returns a list of edits.
 */
function checkForModifications () {
  const pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : {};
  const pogoStops = pogoData && pogoData.pokestops ? pogoData.pokestops : {};
  const keysStops = Object.keys(pogoStops);
  const pogoGyms = pogoData && pogoData.gyms ? pogoData.gyms : {};
  const keysGyms = Object.keys(pogoGyms);
  let changeList = [];
  if (pogoData) {
    Object.values(pNavData.pokestop).forEach(function (stop) {

      /** @type {editData}*/
      let detectedChanges = {edits: {}};
      let newData;
      if (!keysStops.includes(stop.guid)) {
        if (keysGyms.includes(stop.guid)) {
          detectedChanges.edits.type = 'gym';
          newData = pogoGyms[stop.guid];
          if (newData.isEx) {
            detectedChanges.edits.ex_eligible = 1;
          }
        } else {
          detectedChanges.edits.type = 'none';
        }
      } else {
        newData = pogoStops[stop.guid];
      }
      // compare data
      if (newData) {
        if (newData.name !== stop.name) {
          detectedChanges.edits.name = newData.name;
        }
        // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in Pogo Tools. Maybe there's a bug with that...
        if (newData.lat != stop.lat) {
          detectedChanges.edits.latitude = newData.lat;
        }
        // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in Pogo Tools. Maybe there's a bug with that...
        if (newData.lng != stop.lng) {
          detectedChanges.edits.longitude = newData.lng;
        }
      }
      if (Object.keys(detectedChanges.edits).length > 0) {
        detectedChanges.oldName = stop.name;
        detectedChanges.oldType = 'pokestop';
        detectedChanges.guid = stop.guid;
        detectedChanges.lat = stop.lat;
        detectedChanges.lng = stop.lng;
        changeList.push(detectedChanges);
      }
    });
    Object.values(pNavData.gym).forEach(function (gym) {

      /** @type {editData}*/
      let detectedChanges = {edits: {}};
      let newData;
      if (!keysGyms.includes(gym.guid)) {
        if (keysStops.includes(gym.guid)) {
          detectedChanges.edits.type = 'pokestop';
          newData = pogoStops[gym.guid];
        } else {
          detectedChanges.edits.type = 'none';
        }
      } else {
        newData = pogoGyms[gym.guid];
      }
      // compare data
      if (newData) {
        if (newData.name !== gym.name) {
          detectedChanges.edits.name = newData.name;
        }
        // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in Pogo Tools. Maybe there's a bug with that...
        if (newData.lat != gym.lat) {
          detectedChanges.edits.latitude = newData.lat;
        }
        // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in Pogo Tools. Maybe there's a bug with that...
        if (newData.lng != gym.lng) {
          detectedChanges.edits.longitude = newData.lng;
        }
        if (Boolean(newData.isEx) !== Boolean(gym.isEx)) {
          // that treats undefined as false, otherwise undefined and false would be unequal, even with != instead of !==.
          const newEx = newData.isEx ? newData.isEx : false;
          detectedChanges.edits.ex_eligible = newEx ? 1 : 0;
        }
      }
      if (Object.keys(detectedChanges.edits).length > 0) {
        detectedChanges.oldName = gym.name;
        detectedChanges.oldType = 'gym';
        detectedChanges.guid = gym.guid;
        detectedChanges.lat = gym.lat;
        detectedChanges.lng = gym.lng;
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
 * @property {'pokestop'|'gym'} oldType
 * @property {string} oldName
 * @property {string} guid
 * @property {string} lat
 * @property {string} lng
 * @property {object} edits
 * @property {string} [edits.latitude]
 * @property {string} [edits.longitude]
 * @property {string} [edits.name]
 * @property {'pokestop'|'gym'|'none'} [edits.type]
 * @property {number} [edits.ex_eligible]
 */

/**
 * data about portals
 * @typedef {object} portalData
 * @property {'pokestop'|'gym'} type
 * @property {string} guid
 * @property {string} name
 * @property {string} lat
 * @property {string} lng
 * @property {boolean} [isEx]
 */

/**
 * data like it is stored in PoGoTools Plugin (mainly portalData without type field).
 * @external
 * @typedef {object} pogoToolsData
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
  let changes = {edits: {}};

  /** @type {portalData} */
  let savedData;
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
    changes.edits.type = currentData.type;
  }
  if (currentData.lat != savedData.lat) {
    changes.edits.latitude = currentData.lat.toString();
  }
  if (currentData.lng != savedData.lng) {
    changes.edits.longitude = currentData.lng.toString();
  }
  if (currentData.name != savedData.name) {
    changes.edits.name = currentData.name;
  }
  if (Boolean(currentData.isEx) !== Boolean(savedData.isEx)) {
    // to cope with undefined == false etc.
    changes.edits.ex_eligible = currentData.isEx ? 1 : 0;
  }
  if (Object.keys(changes.edits).length > 0) {
    changes.oldName = savedData.name;
    changes.oldType = savedData.type;
    changes.guid = savedData.guid;
    changes.lat = savedData.lat;
    changes.lng = savedData.lng;
    return changes;
  } else {
    return null;
  }
}

/**
 * fetch previous data from local storage and add
 * @param {'pokestop'|'gym'} type - expected values pokestop or gym
 * @return {pogoToolsData[] | null} returns the data to export or null if Pogo Tools Data was not found.
 */
function gatherExportData (type) {

  /** @type {pogoToolsData[]}*/
  let pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : {};
  if (pogoData[`${type}s`]) {
    pogoData = Object.values(pogoData[`${type}s`]);
    const doneGuids = Object.keys(pNavData.pokestop).concat(Object.keys(pNavData.gym));
    const distanceNotCheckable = settings.lat === null || settings.lng === null || settings.radius === null;

    /** @type {pogoToolsData[]} */
    let exportData = pogoData.filter(function (object) {
      return (
        !doneGuids.includes(object.guid) &&
        (distanceNotCheckable || checkDistance(object.lat, object.lng, settings.lat, settings.lng) <= settings.radius)
      );
    });
    return exportData;
  }
  return null;
}

/**
 * @param {'pokestop'|'gym'} type - the type of locations to export (expected values pokestop or gym)
 */
function bulkExport (type) {
  if (!timer) {
    let data = gatherExportData(type);
    if (!data) {
      alert(getString('alertProblemPogoTools', settings.language));
      return;
    }
    if (settings.useBot) {
      botExport(data, type); // jump to BotExport immediately before opening the dialog, this is not needed!
      return;
    }
    let i = 0;
    window.onbeforeunload = function () {
      saveState(data, type, i);
      return null;
    };

    timer = setInterval(() => {
      if (i < data.length && data.length > 0) {
        normalExport(data, type, thisDialog, i).then((result) => {
          i = result;
        });
      } else {
        saveState(data, type, i);
        clearInterval(timer);
        timer = null;
        window.onbeforeunload = null;
      }
    }, wait);

    let dialog = window.dialog({
      id: 'bulkExportProgress',
      html: bulkExportDialog({
        language: settings.language,
        max: data.length
      }),
      width: 'auto',
      title: getString('bulkExportProgressTitle', settings.language),
      buttons: {
        OK: {
          text: getString('bulkExportProgressButtonText', settings.language),
          title: getString('bulkExportProgressButtonTitle', settings.language),
          click () {
            saveState(data, type, i);
            clearInterval(timer);
            timer = null;
            // eslint-disable-next-line no-underscore-dangle
            if (window._current_highlighter === getString('portalHighlighterName', settings.language)) {
              // re-validate highlighter if it is enabled.
              window.changePortalHighlights(getString('portalHighlighterName', settings.language));
            }
            dialog.dialog('close');
          }
        }
      }
    });
    let thisDialog = dialog.parent();

    $('.ui-button.ui-dialog-titlebar-button-close', thisDialog).on('click', function () {
      saveState(data, type, i);
      clearInterval(timer);
      timer = null;
    });
    if (data.length > 0) {
      normalExport(data, type, dialog, 0).then((result) => {
        i = result;
      }); // start immediately instead of waiting 2 seconds.
    } else {
      updateExportDialog(thisDialog, 0, 0, 0);
    }
  } else {
    console.error('Bulk Export already running!');
  }
}

/**
 * One Export step when the Companion Discord bot should be used.
 * @param {pogoToolsData[]} data all locations that need exporting
 * @param {string} type the location type of the given data
 */
function botExport (data, type) {

  /** @type {portalData[]} */
  let exportdata = [...data];
  exportdata.forEach((element) => {
    element.type = type; // convert pogoToolsData to portalData
  });
  let formData = new FormData();
  let date = new Date();
  formData.append('content', `<@${companionId}> cm`);
  formData.append('username', settings.name);
  formData.append(
    'file',
    new Blob([JSON.stringify(exportdata, null, 2)], {type: 'application/json'}),
    `creations-${settings.name}-${date.getUTCFullYear()}-${date.getUTCMonth()}-${date.getUTCDate()}_${date.getUTCHours()}:${date.getUTCMinutes()}.json`
  );
  $.ajax({
    method: 'POST',
    url: settings.webhookUrl,
    contentType: false,
    processData: false,
    data: formData,
    error (jgXHR, textStatus, errorThrown) {
      console.error(`${textStatus} - ${errorThrown}`);
    },
    success () {
      saveState(data, type);
      // eslint-disable-next-line no-underscore-dangle
      if (window._current_highlighter === getString('portalHighlighterName', settings.language)) {
        // re-validate highlighter if it is enabled.
        window.changePortalHighlights(getString('portalHighlighterName', settings.language));
      }
    }
  });
}

/**
 * One Export step when only the WebHook should be used.
 * @async
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
  let entry = data[i];
  let lat = entry.lat;
  let lng = entry.lng;
  // escaping Hyphens in Portal Names
  let name = entry.name;
  let prefix = `<@${pNavId}> `;
  let ex = Boolean(entry.isEx);
  let options = ex ? ' "ex_eligible: 1"' : '';
  let content = `${prefix}create poi ${type} «${name}» ${lat} ${lng}${options}`;
  const params = {
    username: settings.name,
    avatar_url: '',
    content
  };
  let success = await fetch(settings.webhookUrl, {
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
 * assembles the edit data for the companion bot and sends it.
 * @param {editData[]} [changes] - optional list of changes that should be transferred.
 */
function botEdit (changes) {
  if (settings.webhookUrl === null) {
    console.error('no Webhook URL present!');
    return;
  }
  if (typeof changes == 'undefined') {
    changes = checkForModifications();
  }
  if (changes.length == 0) {
    console.log('nothing to export!');
    return;
  }
  let data = new FormData();
  let date = new Date();
  data.append('content', `<@${companionId}> e`);
  data.append('username', settings.name);
  data.append(
    'file',
    new Blob([JSON.stringify(changes, null, 2)], {type: 'application/json'}),
    `edits-${settings.name}-${date.getUTCFullYear()}-${date.getUTCMonth()}-${date.getUTCDate()}_${date.getUTCHours()}:${date.getUTCMinutes()}.json`
  );
  $.ajax({
    method: 'POST',
    url: settings.webhookUrl,
    contentType: false,
    processData: false,
    data,
    error (jgXHR, textStatus, errorThrown) {
      console.error(`${textStatus} - ${errorThrown}`);
    },
    success () {
      updateDone(changes);
      // eslint-disable-next-line no-underscore-dangle
      if (window._current_highlighter === getString('portalHighlighterName', settings.language)) {
        // re-validate highlighter if it is enabled.
        window.changePortalHighlights(getString('portalHighlighterName', settings.language));
      }
    }
  });
}

/**
 * updates the bulk export dialog (called by the specific export functions).
 * @param {HTMLElement} dialog the export dialog
 * @param {number} cur current export state
 * @param {number} max count of all elements that need exporting
 * @param {number} time remaining time
 */
function updateExportDialog (dialog, cur, max, time) {
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
    $('#exportState', dialog).text(getString('exportStateTextReady', settings.language));
    const okayButton = $('.ui-button', dialog.parentElement).not('.ui-dialog-titlebar-button');
    okayButton.text('OK');
    okayButton.prop('title', '');
  }
}

function modifyPortalDetails (data) {
  const detailsObserver = new MutationObserver(waitForPogoButtons);
  const statusObserver = new MutationObserver(waitForPogoStatus);
  const send = Boolean(settings.webhookUrl);
  console.log(data);
  let guid = data.guid;
  selectedGuid = guid;
  if (!window.plugin.pogo) {
    window.removeHook('portalDetailsUpdated', modifyPortalDetails);
    setTimeout(function () {
      $('#portaldetails').append(portalDetails({
        language: settings.language,
        options: {send},
        mobile: window.isSmartphone()
      }));
      $('#pnavCopier').on('click', () => copy());
      if (pNavData.pokestop[selectedGuid]) {
        $('#PNavStop').prop('checked', true);
      } else if (pNavData.gym[selectedGuid]) {
        if (pNavData.gym[selectedGuid].isEx) {
          $('#PNavEx').prop('checked', true);
        } else {
          $('#PNavGym').prop('checked', true);
        }
      }
      window.addHook('portalDetailsUpdated', modifyPortalDetails);
    }, 0);
  } else {
    // wait for the Pogo Buttons to get added
    detailsObserver.observe($('#portaldetails')[0], {childList: true});
    // if running on mobile, also wait for the Buttons in Status bar to get added and add it there.
    if (window.isSmartphone()) {
      statusObserver.observe($('.PogoStatus')[0], {childList: true});
    }
  }
}

function waitForPogoButtons (mutationList, invokingObserver) {
  mutationList.forEach(function (mutation) {
    if (mutation.type === 'childList' && mutation.addedNodes) {
      for (const node of mutation.addedNodes) {
        if (node.className == 'PogoButtons') {
          $(node).after(`
             <a id="pnavCopier" style="position:absolute;right:5px" title="${getString('PogoButtonsTitle', settings.language, {
    send: Boolean(settings.webhookUrl)
  })}" accesskey="p">${getString('PogoButtonsText', settings.language, {send: Boolean(settings.webhookUrl)})}</a>
             `);
          $('#pnavCopier').on('click', () => copy());
          $(node).css('display', 'inline');
          // we don't need to look for the class anymore because we just found what we wanted ;-)
          invokingObserver.disconnect();
          break;
        }
      }
    }
  });
}

function waitForPogoStatus (mutationList, invokingObserver) {
  mutationList.forEach(function (mutation) {
    if (mutation.type === 'childList' && mutation.addedNodes.length > 0) {
      $('.PogoStatus').append(`<a id="pnavStatusCopier" style="position:absolute;right:5px">${getString('PogoButtonsText', settings.language, {send: Boolean(settings.webhookUrl)})}</a>`);
      $('#pnavStatusCopier').on('click', () => copy());
      invokingObserver.disconnect();
    }
  });
}

function setup () {
  $('head').append(/* html */ `<style>
      .Button {
        border: 1px solid #FFCE00;
        padding: 2px;
        background-color: rgba(8, 48, 78, 0.9);
        min-width: 40px;
        color: #FFCE00;
        text-decoration: none!important;
        cursor: default;
      }
      .form-group {
        margin-bottom: 10px;
      }
    </style>`);
  if (localStorage['plugin-pnav-settings']) {
    let savedSettings = JSON.parse(localStorage.getItem('plugin-pnav-settings'));
    Object.keys(settings).forEach((key) => {
      if (typeof savedSettings[key] !== 'undefined') {
        settings[key] = savedSettings[key];
      }
    });
    localStorage['plugin-pnav-settings'] = JSON.stringify(settings);
    // settings = JSON.parse(localStorage.getItem('plugin-pnav-settings'));
  }
  if (!settings.language) {
    settings.language = detectLanguage();
    localStorage['plugin-pnav-settings'] = JSON.stringify(settings);
  }
  if (localStorage['plugin-pnav-done-pokestop']) {
    pNavData.pokestop = JSON.parse(localStorage.getItem('plugin-pnav-done-pokestop'));
  }
  if (localStorage['plugin-pnav-done-gym']) {
    pNavData.gym = JSON.parse(localStorage.getItem('plugin-pnav-done-gym'));
  }
  $('#toolbox').append(`<a id="pNavSettings" title="${getString('pokeNavSettingsTitle', settings.language)}" accesskey="s">${getString('pokeNavSettingsText', settings.language)}</a>`);
  $('#pNavSettings').on('click', () => {
    if (!timer)showSettings();
  });
  $('body').prepend('<input id="copyInput" style="position: absolute;"></input>');
  lCommBounds = new L.LayerGroup();
  if (settings.lat && settings.lng && settings.radius) {
    let commCircle = L.circle(L.latLng([
      settings.lat,
      settings.lng
    ]), {
      radius: settings.radius * 1000,
      interactive: false,
      fillOpacity: 0.1,
      color: '#000000'
    });
    lCommBounds.addLayer(commCircle);
  }
  window.addLayerGroup(getString('lCommBoundsName', settings.language), lCommBounds);
  window.addPortalHighlighter(getString('portalHighlighterName', settings.language), highlight);
  let isLinksDisplayed = window.isLayerGroupDisplayed('Links', false);
  let isFieldsDisplayed = window.isLayerGroupDisplayed('Fields', false);
  $('#portal_highlight_select').on('change', function () {
    // eslint-disable-next-line no-underscore-dangle
    if (window._current_highlighter === getString('portalHighlighterName', settings.language)) {
      isLinksDisplayed = window.isLayerGroupDisplayed('Links', false);
      isFieldsDisplayed = window.isLayerGroupDisplayed('Fields', false);
      // eslint-disable-next-line no-underscore-dangle
      const layers = window.layerChooser._layers;
      const layerIds = Object.keys(layers);
      layerIds.forEach(function (id) {
        const layer = layers[id];
        if (layer.name === 'Links' || layer.name === 'Fields') {
          window.map.removeLayer(layer.layer);
        } else if (
          ((/Level . Portals/).test(layer.name) || layer.name === 'Resistance' || layer.name === 'Enlightened' || layer.name === 'Unclaimed/Placeholder Portals') &&
          !window.isLayerGroupDisplayed(layer.name, false)
        ) {
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
  window.addHook('portalDetailsUpdated', modifyPortalDetails);
}

// register plugin
setup.info = SCRIPT_INFO;
if (!window.bootPlugins) window.bootPlugins = [];
window.bootPlugins.push(setup);
// if IITC has already booted, immediately run the 'setup' function
if (window.iitcLoaded && typeof setup === 'function') setup();
