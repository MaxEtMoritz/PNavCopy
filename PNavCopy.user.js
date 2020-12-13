// ==UserScript==
/* globals $, GM_info */
// eslint-disable-next-line multiline-comment-style
// @id             pnavcopy@maxetmoritz
// @name           IITC plugin: Copy PokeNav Command
// @category       Misc
// @downloadURL    https://raw.github.com/MaxEtMoritz/PNavCopy/main/PNavCopy.user.js
// @author         MaxEtMoritz
// @version        1.4.2
// @namespace      https://github.com/MaxEtMoritz/PNavCopy
// @description    Copy portal info to clipboard or send it to Discord in the format the PokeNav Discord bot needs.
// @include        http*://intel.ingress.com/*
// @grant          none
// ==/UserScript==

/* eslint no-unused-vars: 1 */
/* eslint brace-style: ["error","1tbs"] */
/* eslint semi: ["error", "always"] */
/* eslint quotes: ["error", "single", {"avoidEscape": true, "allowTemplateLiterals": true}] */
/* eslint keyword-spacing: ["error"] */
/* eslint spaced-comment:["error", "always"] */
/* eslint no-trailing-spaces: ["error"] */
/* eslint prefer-template: "warn" */
/* eslint comma-style: "error" */
/* eslint function-call-argument-newline: ["error", "consistent"] */
/* eslint indent: ["error", 2] */

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
  window.plugin.pnav.selectedGuid = null;
  window.plugin.pnav.settings = {
    webhookUrl: '',
    name: window.PLAYER.nickname,
    radius: '',
    lat: '',
    lng: '',
    prefix: '$'
  };
  window.plugin.pnav.request = new XMLHttpRequest();
  window.plugin.pnav.copy = function () {
    var input = $('#copyInput');
    if (window.selectedPortal) {
      var portal = window.portals[window.plugin.pnav.selectedGuid];
      // escaping Backslashes and Hyphens in Portal Names
      /** @type {string} */
      var name = portal.options.data.title
        .replaceAll('\\', '\\\\')
        .replaceAll('"', '\\"');
      var latlng = portal.getLatLng();
      var lat = latlng.lat;
      var lng = latlng.lng;
      var opt = ' ';
      var type = '';

      /** @type {string} */
      var prefix = window.plugin.pnav.settings.prefix;
      if (window.plugin.pogo) {
        if ($('.pogoStop span').css('background-position') == '100% 0%') {
          type = 'pokestop';
        } else if ($('.pogoGym span').css('background-position') == '100% 0%') {
          type = 'gym';
          if ($('#PogoGymEx').prop('checked') == true) {
            opt += '"ex_eligible: 1"';
          }
        }
      } else {
        if (document.getElementById('PNavEx').checked) {
          type = 'gym';
          opt += '"ex_eligible: 1"';
        } else if (document.getElementById('PNavGym').checked) {
          type = 'gym';
        } else {
          type = 'pokestop';
        }
      }
      var done = localStorage[`plugin-pnav-done-${type}`] ? JSON.parse(localStorage[`plugin-pnav-done-${type}`]) : {};
      if (
        window.plugin.pnav.settings.lat &&
        window.plugin.pnav.settings.lng &&
        window.plugin.pnav.settings.radius &&
        checkDistance(lat, lng, window.plugin.pnav.settings.lat, window.plugin.pnav.settings.lng) >
        window.plugin.pnav.settings.radius
      ) {
        alert('this location is outside the specified Community Area!');
      } else if (
        done[window.plugin.pnav.selectedGuid]
      ) {
        alert('this location has already been exported! If you are sure this is not the case, try resetting the Export State in settings.');
      } else {
        if (window.plugin.pnav.settings.webhookUrl) {
          sendMessage(`${prefix}create poi ${type} "${name}" ${lat} ${lng}${opt}`);
          let pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : null;
          if (pogoData && pogoData[type] && pogoData[`${type}.${window.plugin.pnav.selectedGuid}`]) {
            done[window.plugin.pnav.selectedGuid] = pogoData[window.plugin.pnav.selectedGuid];
          } else {
            var newObject = {
              'guid': String(window.plugin.pnav.selectedGuid),
              'name': String(portal.options.data.title),
              'lat': String(lat),
              'lng': String(lng)
            };
            if ($('#PNavEx').prop('checked')) {
              newObject.isEx = true;
            }
            done[window.plugin.pnav.selectedGuid] = newObject;
          }
          localStorage[`plugin-pnav-done-${type}`] = JSON.stringify(done);
          console.log('sent!');
        } else {
          input.show();
          input.val(`${prefix}create poi ${type} "${name}" ${lat} ${lng}${opt}`);
          copyfieldvalue('copyInput');
          input.hide();
        }
      }
    }
  };

  window.plugin.pnav.showSettings = function () {
    var validURL = '^https?://discord(app)?.com/api/webhooks/[0-9]*/.*';
    var html = `
        <p id="prefix">
          <label title="Input the Prefix for the PokeNav Bot here. Default Prefix is $.">
            PokeNav Prefix:
            <input type="text" id="pnavprefix" pattern="^.$" value="${window.plugin.pnav.settings.prefix}" placeholder="$" style="width:15px"/>
          </label>
        </p>
        <p id="webhook"><label title="Paste the URL of the WebHook you created in your Server's Admin Channel here. If left blank, the Commands are copied to Clipboard.">
            Discord WebHook URL:
            <input type="url" style="width:100%" id="pnavhookurl" value="${window.plugin.pnav.settings.webhookUrl}" pattern="${validURL}"/>
        </label></p>
        <p>
          <Label title="The Name that will be displayed if you send to the PokeNav channel. Default is your Ingess Codename.">
            Name:
            <input id="pnavCodename" type="text" placeholder="${window.PLAYER.nickname}" value="${window.plugin.pnav.settings.name}"/>
          </label>
        </p>
        <p>
          <label id="center" title="Paste the Center Coordinate of your Community here (you can view it typing ${window.plugin.pnav.settings.prefix}show settings in Admin Channel)">
          Community Center:
          <input id="pnavCenter" type="text" pattern="^-?&#92;d?&#92;d(&#92;.&#92;d+)?, -?&#92;d?&#92;d(&#92;.&#92;d+)?" value="${window.plugin.pnav.settings.lat != '' ? `${window.plugin.pnav.settings.lat}, ${window.plugin.pnav.settings.lng}` : ''}"/>
          </label>
          <br>
          <label id="radius" title="Enter the specified Community Radius here.">
          Community Radius:
          <input id="pnavRadius" style="width:70px" type="number" min="0" step="0.001" value="${window.plugin.pnav.settings.radius}"/>
          </label>
        </p>
        <p><button type="Button" style="width:100%" title="erase all Export History" onclick="window.plugin.pnav.deleteExportState();return false;">Erase Location Export History</button></p>
        `;
    if (window.plugin.pogo) {
      html += `
            <p><button type="Button" id="btnBulkExportGyms" style="width:100%" title="Grab the File where all Gyms are stored by PoGoTools and send them one by one via Web Hook. This can take much time!" onclick="window.plugin.pnav.bulkExport('gym');return false;">Export all PogoTools Gyms</button></p>
            <p><button type="Button" id="btnBulkExportStops" style="width:100%" title="Grab the File where all Stops are stored by PoGoTools and send them one by one via Web Hook. This can take much time!" onclick="window.plugin.pnav.bulkExport('pokestop');return false;">Export all PogoTools Stops</button></p>
            <p><button type="Button" style="width:100%" title="Check if the Pogo Tools Data was modified and start Upload process of modifications" onclick="window.plugin.pnav.bulkModify();return false;">Check for Modifications</button></p>
            `;
    }

    const container = window.dialog({
      id: 'pnavsettings',
      width: 'auto',
      height: 'auto',
      html,
      title: 'PokeNav Settings',
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
              $('#webhook').after('<label id="lblErrorWH" style="color:red">invalid URL! please delete or correct it!</label>');
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
              $('#prefix').after(`<label id="lblErrorPf" style="color:red">Prefix must be only one Character!</label>`);
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
              $('#radius').after(`<label id="lblErrorRd" style="color:red"><br>Invalid Radius! Please check if it is a valid Number!</label>`);
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
              new RegExp('^-?\\d?\\d(\\.\\d+)?, -?\\d?\\d(\\.\\d+)?$').test($('#pnavCenter').val()) &&
              lat >= -90 &&
              lat <= 90 &&
              lng >= -90 &&
              lng <= 90
            ) {
              window.plugin.pnav.settings.lat = lat;
              window.plugin.pnav.settings.lng = lng;
              if ($('#lblErrorCn').length > 0) {
                $('#lblErrorCn').remove();
              }
            } else {
              if ($('#lblErrorCn').length == 0) {
                $('#center').after('<label id="lblErrorCn" style="color:red"><br>Invalid Coordinate Format! Please input them like 00.0...00, 00.0...00!</label>');
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
            alert('Settings not saved because Export was running. Pause the Export and then try again!');
            container.dialog('close');
          }
        }
      }
    });
  };

  window.plugin.pnav.deleteExportState = function () {
    if (localStorage['plugin-pnav-done-pokestop']) {
      localStorage.removeItem('plugin-pnav-done-pokestop');
    }
    if (localStorage['plugin-pnav-done-gym']) {
      localStorage.removeItem('plugin-pnav-done-gym');
    }
  };

  /**
   * saves the State of the Bulk Export to local Storage.
   * @param {object[]} data
   * @param {string} type
   * @param {number} index
   */
  function saveState (data, type, index) {

    /** @type {object} */
    var done = localStorage[`plugin-pnav-done-${type}`]
      ? JSON.parse(localStorage[`plugin-pnav-done-${type}`])
      : {};
    const addToDone = data.slice(0, index);
    // console.log(addToDone);
    addToDone.forEach(function (object) {
      done[object.guid] = object;
    });
    localStorage.setItem(
      `plugin-pnav-done-${type}`,
      JSON.stringify(done)
    );
  }

  window.plugin.pnav.bulkModify = function () {
    if (window.plugin.pogo) {
      const changeList = checkForModifications();
      console.log(changeList);
      const html = `
        <h3>
          The following Poi was modified in PoGo Tools:
        </h3>
        <h3 id="pNavOldPoiName"/>
        <label>The following has changed:</label>
        <ul id="pNavChangesMade"/>
        <label>
          PokeNav ID:
          <input id="pNavPoiId" type="number" min="0" step="1"/>
        </label>
        <br>
        <button type="Button" class="ui-button" id="pNavPoiInfo" title="Sends the PoI Information Command for the PoI.">
          Send PoI Info Command
        </button>
        <button type="Button" class="ui-button" id="pNavModCommand" title="You must input the PokeNav location ID before you can send the modification command!" style="color:darkgray;cursor:default;text-decoration:none">
          Send Modification Command
        </button>
      `;
      if (changeList.length > 0) {
        const modDialog = window.dialog({
          id: 'pNavmodDialog',
          title: 'PokeNav Bulk Modification',
          html,
          width: 'auto',
          height: 'auto',
          buttons: {
            'Skip one' () {
              i++;
              if (i == changeList.length) {
                modDialog.dialog('close');
              } else {
                poi = changeList[i];
                updateUI(modDialog, poi);
              }
            }
          }
        });
        var i = 0;
        var poi = changeList[i];
        $('#pNavPoiInfo', modDialog).on('click', function () {
          sendMessage(`${window.plugin.pnav.settings.prefix}${poi.oldType}-info ${poi.oldName}`);
        });
        $('#pNavPoiId', modDialog).on('input', function (e) {
          // console.log(e);
          const valid = e.target.validity.valid;
          const value = e.target.valueAsNumber;
          if (valid && value && value > 0) {
            $('#pNavModCommand', modDialog).prop('style', '');
            $('#pNavModCommand', modDialog).prop('title', 'Send the PoI Modification Command to Discord');
          } else {
            $('#pNavModCommand', modDialog).css('color', 'darkgray');
            $('#pNavModCommand', modDialog).css('cursor', 'default');
            $('#pNavModCommand', modDialog).css('text-decoration', 'none');
            $('#pNavModCommand', modDialog).css('border', '1px solid darkgray');
            $('#pNavModCommand', modDialog).prop('title', 'You must input the PokeNav location ID before you can send the modification command!');
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
              updateUI(modDialog, poi);
            }
          }
        });
        updateUI(modDialog, poi);
        // TODO design Dialog, send stop info message and trigger sendModCommand then.
      } else {
        alert('No modifications of PogoTools data detected!');
      }
    }
  };

  function updateDone (poi) {
    const pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : {};
    const pogoGyms = pogoData.gyms ? pogoData.gyms : {};
    const pogoStops = pogoData.stops ? pogoData.stops : {};
    const pNavStops = localStorage['plugin-pnav-done-pokestop'] ? JSON.parse(localStorage['plugin-pnav-done-pokestop']) : {};
    const pNavGyms = localStorage['plugin-pnav-done-gym'] ? JSON.parse(localStorage['plugin-pnav-done-gym']) : {};
    if (Object.keys(pogoStops).includes(poi.guid)) {
      pNavStops[poi.guid] = pogoStops[poi.guid];
      if (Object.keys(pNavGyms).includes(poi.guid)) {
        delete pNavGyms[poi.guid];
        localStorage['plugin-pnav-done-gym'] = JSON.stringify(pNavGyms);
      }
      localStorage['plugin-pnav-done-pokestop'] = JSON.stringify(pNavStops);
    } else if (Object.keys(pogoGyms).includes(poi.guid)) {
      debugger;
      pNavGyms[poi.guid] = pogoGyms[poi.guid];
      if (Object.keys(pNavStops).includes(poi.guid)) {
        delete pNavStops[poi.guid];
        localStorage['plugin-pnav-done-pokestop'] = JSON.stringify(pNavStops);
      }
      localStorage['plugin-pnav-done-gym'] = JSON.stringify(pNavGyms);
    } else {
      if (poi.oldType === 'gym') {
        delete pNavGyms[poi.guid];
        localStorage['plugin-pnav-done-gym'] = JSON.stringify(pNavGyms);
      } else {
        delete pNavStops[poi.guid];
        localStorage['plugin-pnav-done-pokestop'] = JSON.stringify(pNavStops);
      }
    }
  }

  /*
   *function updateDone (poi) {
   *  console.log('hiu');
   *  debugger;
   *  // Does not work yet.
   *  var stop = localStorage['plugin-pnav-done-pokestop'] ? JSON.parse(localStorage['plugin-pnav-done-pokestop']) : {};
   *  var gym = localStorage['plugin-pnav-done-gym'] ? JSON.parse(localStorage['plugin-pnav-done-gym']) : {};
   *  if (poi.oldType === 'stop') {
   *    if (poi.type) {
   *      if (poi.type === 'none') {
   *        // delete it
   *        delete stop[poi.guid];
   *        localStorage['plugin-pnav-done-pokestop'] = JSON.stringify(stop);
   *        return;
   *      } else {
   *        // swap type
   *        let currpoi = stop[poi.guid];
   *        delete stop[poi.guid];
   *        gym[poi.guid] = currpoi;
   *        localStorage['plugin-pnav-done-pokestop'] = JSON.stringify(stop);
   *        if (poi.latitude) {
   *          gym[poi.guid].lat = poi.latitude;
   *        }
   *        if (poi.longitude) {
   *          gym[poi.guid].lng = poi.longitude;
   *        }
   *        if (poi.name) {
   *          gym[poi.guid].name = poi.name;
   *        }
   *        if (poi.ex_eligible || poi.ex_eligible === 0) {
   *          gym[poi.guid].isEx = Boolean(poi.ex_eligible);
   *        }
   *        localStorage['plugin-pnav-done-gym'] = JSON.stringify(gym);
   *        return;
   *      }
   *    }
   *    if (poi.latitude) {
   *      stop[poi.guid].lat = poi.latitude;
   *    }
   *    if (poi.longitude) {
   *      stop[poi.guid].lng = poi.longitude;
   *    }
   *    if (poi.name) {
   *      stop[poi.guid].name = poi.name;
   *    }
   *    localStorage['plugin-pnav-done-pokestop'] = JSON.stringify(stop);
   *  } else {
   *    if (poi.type) {
   *      if (poi.type === 'none') {
   *        // delete it
   *        delete gym[poi.guid];
   *        localStorage['plugin-pnav-done-gym'] = JSON.stringify(gym);
   *        return;
   *      } else {
   *        // swap type
   *        let currpoi = gym[poi.guid];
   *        delete gym[poi.guid];
   *        stop[poi.guid] = currpoi;
   *        localStorage['plugin-pnav-done-gym'] = JSON.stringify(gym);
   *        if (poi.latitude) {
   *          stop[poi.guid].lat = poi.latitude;
   *        }
   *        if (poi.longitude) {
   *          stop[poi.guid].lng = poi.longitude;
   *        }
   *        if (poi.name) {
   *          stop[poi.guid].name = poi.name;
   *        }
   *        if (poi.ex_eligible || poi.ex_eligible === 0) {
   *          stop[poi.guid].isEx = Boolean(poi.ex_eligible);
   *        }
   *        localStorage['plugin-pnav-done-pokestop'] = JSON.stringify(stop);
   *        return;
   *      }
   *    }
   *    if (poi.latitude) {
   *      gym[poi.guid].lat = poi.latitude;
   *    }
   *    if (poi.longitude) {
   *      gym[poi.guid].lng = poi.longitude;
   *    }
   *    if (poi.name) {
   *      gym[poi.guid].name = poi.name;
   *    }
   *    if (poi.ex_eligible || poi.ex_eligible === 0) {
   *      gym[poi.guid].isEx = Boolean(poi.ex_eligible);
   *      console.log('been there!');
   *    }
   *    localStorage['plugin-pnav-done-gym'] = JSON.stringify(gym);
   *  }
   *}
   */
  function updateUI (dialog, poi) {
    $('#pNavOldPoiName', dialog).text(poi.oldName);
    $('#pNavChangesMade', dialog).empty();
    for (const [
      key,
      value
    ] of Object.entries(poi)) {
      if (key !== 'oldName' && key !== 'oldType' && key !== 'guid') {
        $('#pNavChangesMade', dialog).append(`<li>${key}=> ${value}</li>`);
      }
    }
    $('#pNavPoiId', dialog).val('');
    $('#pNavModCommand', dialog).css('color', 'darkgray');
    $('#pNavModCommand', dialog).css('border', '1px solid darkgray');
    $('#pNavModCommand', dialog).css('cursor', 'default');
    $('#pNavModCommand', dialog).css('text-decoration', 'none');
    $('#pNavModCommand', dialog).prop('title', 'You must input the PokeNav location ID before you can send the modification command!');
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
          command += ` "${key}: ${value}"`;
        }
      }
    }
    sendMessage(command);
  }

  function checkForModifications () {
    const pNavStops = localStorage['plugin-pnav-done-pokestop'] ? JSON.parse(localStorage['plugin-pnav-done-pokestop']) : {};
    const pNavGyms = localStorage['plugin-pnav-done-gym'] ? JSON.parse(localStorage['plugin-pnav-done-gym']) : {};
    const pogoData = localStorage['plugin-pogo'] ? JSON.parse(localStorage['plugin-pogo']) : {};
    const pogoStops = (pogoData && pogoData.pokestops) ? pogoData.pokestops : {};
    console.log(pogoStops);
    const keysStops = Object.keys(pogoStops);
    const pogoGyms = pogoData && pogoData.gyms ? pogoData.gyms : {};
    const keysGyms = Object.keys(pogoGyms);
    var changeList = [];
    if (pNavStops && pogoData) {
      Object.values(pNavStops).forEach(function (stop) {
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
          // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in PogoTools. Maybe there's a bug with that...
          if (originalData.lat != stop.lat) {
            debugger;
            detectedChanges.latitude = originalData.lat;
          }
          // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in PogoTools. Maybe there's a bug with that...
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
    }
    if (pNavGyms && pogoData) {
      Object.values(pNavGyms).forEach(function (gym) {
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
          // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in PogoTools. Maybe there's a bug with that...
          if (originalData.lat != gym.lat) {
            detectedChanges.latitude = originalData.lat;
          }
          // not eqeqeq because sometimes the lat and lng were numbers for me, but most of the time they were strings in PogoTools. Maybe there's a bug with that...
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

  /**
   * fetch previous data from local storage and add
   * @param {string} type - expected values pokestop or gym
   * @return {object[] | null} returns the data to export or null if Pogo Tools Data was not found.
   */
  function gatherExportData (type) {
    var pogoData = JSON.parse(localStorage['plugin-pogo']);
    if (pogoData && pogoData[`${type}s`]) {
      pogoData = Object.values(pogoData[`${type}s`]);
      // console.log(pogoData);

      /** @type object[] */
      const done = localStorage[`plugin-pnav-done-${type}`]
        ? JSON.parse(localStorage[`plugin-pnav-done-${type}`])
        : null;
      const doneGuids = done ? Object.keys(done) : null;
      const distanceNotCheckable =
        !window.plugin.pnav.settings.lat ||
        !window.plugin.pnav.settings.lng ||
        !window.plugin.pnav.settings.radius;
      var exportData = pogoData.filter(function (object) {
        return (
          (!doneGuids || !doneGuids.includes(object.guid)) &&
          (distanceNotCheckable ||
              checkDistance(object.lat, object.lng, window.plugin.pnav.settings.lat, window.plugin.pnav.settings.lng) <= window.plugin.pnav.settings.radius)
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
        alert('There was a problem reading the PogoTools Data File.');
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
            .replaceAll('\\', '\\\\')
            .replaceAll('"', '\\"');
          let prefix = window.plugin.pnav.settings.prefix;
          let ex = Boolean(entry.isEx);
          let options = ex ? ' "ex_eligible: 1"' : '';
          let request = window.plugin.pnav.request;
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
          $('#exportState').text('Export Ready!');
          okayButton.text('OK');
          okayButton.prop('title', '');
          saveState(data, type, i);
          clearInterval(window.plugin.pnav.timer);
          window.plugin.pnav.timer = null;
          window.onbeforeunload = null;
        }
      }, wait);

      var dialog = window.dialog({
        id: 'bulkExportProgress',
        html: `
              <h3 id="exportState">Exporting...</h3>
              <p>
                <label>
                  Progress:
                  <progress id="exportProgressBar" value="0" max="${data.length}"/>
                </label>
              </p>
              <label id="exportNumber">0</label>
              <label> of ${data.length}</label>
              <br>
              <label>Time remaining: </label>
              <label id="exportTimeRemaining">${Math.round((wait * data.length) / 1000)}</label>
              <label>s</label>
        `,
        width: 'auto',
        title: 'PokeNav Bulk Export Progress',
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
      okayButton.text('Pause');
      okayButton.prop(
        'title',
        'store Progress locally and stop Exporting. If you wish to restart, go to Settings and click the Export Button again.'
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
    let request = window.plugin.pnav.request;
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
             <a style="position:absolute;right:5px" title="Copy the PokeNav Command to Clipboard or post to Discord via WebHook" onclick="window.plugin.pnav.copy();return false;" accesskey="c">Copy PokeNav</a>
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
        $('.PogoStatus').append('<a style="position:absolute;right:5px" onclick="window.plugin.pnav.copy();return false;">Copy PokeNav</a>');
        invokingObserver.disconnect();
      }
    });
  }

  var setup = function () {
    console.log('azaza');
    if (localStorage['plugin-pnav-settings']) {
      window.plugin.pnav.settings = JSON.parse(localStorage.getItem('plugin-pnav-settings'));
    }
    $('#toolbox').append('<a title="Configure PokeNav" onclick="if(!window.plugin.pnav.timer){window.plugin.pnav.showSettings();}return false;" accesskey="s">PokeNav Settings</a>');
    $('body').prepend('<input id="copyInput" style="position: absolute;"></input>');
    const detailsObserver = new MutationObserver(waitForPogoButtons);
    const statusObserver = new MutationObserver(waitForPogoStatus);

    window.addHook('portalSelected', function (data) {
      console.log(data);
      var guid = data.selectedPortalGuid;
      window.plugin.pnav.selectedGuid = guid;
      if (!window.plugin.pogo) {
        setTimeout(function () {
          $('#portaldetails').append(`
          <div id="PNav" style="color:#fff;display:flex">
            <Label>
              <input type="radio" checked="true" name="type" value="stop" id="PNavStop"/>
              Stop
            </label>
            <Label>
              <input type="radio" name="type" value="gym" id="PNavGym"/>
              Gym
            </label>
            <Label>
              <input type="radio" name="type" value="ex" id="PNavEx"/>
              Ex Gym
            </label>
            <a style="margin-left:auto;margin-right:5px${window.isSmartphone()?';padding:5px;margin-top:3px;margin-bottom:3px;border:2px outset #20A8B1':''}" title="Copy the PokeNav Command to Clipboard or post to Discord via WebHook" onclick="window.plugin.pnav.copy();return false;" accesskey="c">
              Copy PokeNav
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
