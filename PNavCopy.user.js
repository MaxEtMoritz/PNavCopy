// ==UserScript==
/* globals $, GM_info */
// @id             pnavcopy@maxetmoritz
// @name           IITC plugin: Copy PokeNav Command
// @category       Misc
// @downloadURL    https://github.com/MaxEtMoritz/PNavCopy/releases/download/latest/PNavCopy.user.js
// @author         MaxEtMoritz
// @version        1.4
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
/* eslint linebreak-style: ["error", "windows"] */

// original Plug-In is from https://gitlab.com/ruslan.levitskiy/iitc-mobile/-/tree/master, the License of this project is provided below:

/* ISC License

Copyright © 2013 Stefan Breunig

Permission to use, copy, modify, and/or distribute this software for
any purpose with or without fee is hereby granted, provided that the
above copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL
WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE
AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL
DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA
OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
PERFORMANCE OF THIS SOFTWARE. */

function wrapper(plugin_info) {
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
    prefix: '$',
  };
  window.plugin.pnav.request = new XMLHttpRequest();
  window.plugin.pnav.abort = false;
  window.plugin.pnav.wip = false;
  window.plugin.pnav.copy = function () {
    var input = $('#copyInput');
    if (window.selectedPortal) {
      var portal = window.portals[window.plugin.pnav.selectedGuid];
      var name = portal.options.data.title
        .replaceAll('\\', '\\\\')
        .replaceAll('"', '\\"'); // escaping Backslashes and Hyphens in Portal Names
      var latlng = portal.getLatLng();
      var lat = latlng.lat;
      var lng = latlng.lng;
      var opt = ' ';
      var type = '';
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
      if (window.plugin.pnav.settings.webhookUrl) {
        if (
          window.plugin.pnav.settings.lat &&
          window.plugin.pnav.settings.lng &&
          window.plugin.pnav.settings.radius &&
          checkDistance(lat, lng, window.plugin.pnav.settings.lat, window.plugin.pnav.settings.lng)
          > window.plugin.pnav.settings.radius
        ) {
          alert('this location is outside the specified Community Area!');
        } else {
          sendMessage(`${prefix}create poi ${type} "${name}" ${lat} ${lng}${opt}`);
          console.log('sent!');
        }
      } else {
        input.show();
        input.val(`${prefix}create poi ${type} "${name}" ${lat} ${lng}${opt}`);
        copyfieldvalue('copyInput');
        input.hide();
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
          <input id="pnavCenter" type="text" pattern="^-?&#92;d?&#92;d(&#92;.&#92;d+)?, -?&#92;d?&#92;d(&#92;.&#92;d+)?" value="${window.plugin.pnav.settings.lat != ''? `${window.plugin.pnav.settings.lat}, ${window.plugin.pnav.settings.lng}`: ''}"/>
          </label>
          <br>
          <label id="radius" title="Enter the specified Community Radius here.">
          Community Radius: 
          <input id="pnavRadius" style="width:70px" type="number" min="0" step="0.001" value="${window.plugin.pnav.settings.radius}"/>
          </label>
        </p>
        `;
    if (window.plugin.pogo) {
      html += `
            <p><button type="Button" id="btnBulkExportGyms" style="width:100%" title="Grab the File where all Gyms are stored by PoGoTools and send them one by one via Web Hook. This can take much time!" onclick="window.plugin.pnav.bulkExportGyms();return false;">Export all PogoTools Gyms</button></p>
            <p><button type="Button" id="btnBulkExportStops" style="width:100%" title="Grab the File where all Stops are stored by PoGoTools and send them one by one via Web Hook. This can take much time!" onclick="window.plugin.pnav.bulkExportStops();return false;">Export all PogoTools Stops</button></p>
            `;
    }
    const container = window.dialog({
      id: 'pnavsettings',
      width: 'auto',
      height: 'auto',
      html: html,
      title: 'PokeNav Settings',
      buttons: {
        OK: function () {
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
              $('#webhook').after(
                '<label id="lblErrorWH" style="color:red">invalid URL! please delete or correct it!</label>'
              );
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
            window.plugin.pnav.settings.radius = parseFloat(
              $('#pnavRadius').val()
            );
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
            let arr = $('#pnavCenter').val().split(', ');
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
                $('#center').after(
                  '<label id="lblErrorCn" style="color:red"><br>Invalid Coordinate Format! Please input them like 00.0...00, 00.0...00!</label>'
                );
              }
              allOK = false;
            }
          }
          if (!$('#pnavCodename').val()) {
            window.plugin.pnav.settings.name = window.PLAYER.nickname;
          } else {
            window.plugin.pnav.settings.name = $('#pnavCodename').val();
          }
          if (window.plugin.pnav.wip == false) {
            if (allOK) {
              localStorage.setItem(
                'plugin-pnav-settings',
                JSON.stringify(window.plugin.pnav.settings)
              );
              container.dialog('close');
            }
          } else {
            alert(
              'Settings not saved because Export was running. Pause the Export and then try again!'
            );
            container.dialog('close');
          }
        },
      },
    });
  };

  window.plugin.pnav.bulkExportGyms = function () {
    var data;
    if (localStorage['plugin-pogo']) {
      data = JSON.parse(localStorage.getItem('plugin-pogo'));
    } else if (localStorage['plugin-pogo-data']) {
      data = JSON.parse(localStorage.getItem('plugin-pogo-data'));
    } else {
      alert('Pogo Tools is loaded but no Data File was found!');
    }
    if (data && data.gyms) {
      bulkExport(data.gyms, 'gym');
    }
  };

  window.plugin.pnav.bulkExportStops = function () {
    var data;
    if (localStorage['plugin-pogo']) {
      data = JSON.parse(localStorage.getItem('plugin-pogo'));
    } else if (localStorage['plugin-pogo-data']) {
      data = JSON.parse(localStorage.getItem('plugin-pogo-data'));
    } else {
      alert('Pogo Tools is loaded but no Data File was found!');
    }
    if (data && data.pokestops) {
      bulkExport(data.pokestops, 'pokestop');
    }
  };

  function bulkExport(inData, type) {
    if (window.plugin.pnav.wip == false) {
      // console.log(inData);
      window.plugin.pnav.abort = false;
      window.plugin.pnav.wip = true;
      var data = [];
      if (localStorage.getItem(`plugin-pnav-todo-${  type}`)) {
        data = JSON.parse(localStorage.getItem(`plugin-pnav-todo-${  type}`));
      } else {
        var origKeys = Object.keys(inData);
        var done = localStorage.getItem(`plugin-pnav-done-${  type}`)
          ? JSON.parse(localStorage.getItem(`plugin-pnav-done-${  type}`))
          : null;
        origKeys.forEach(function (key) {
          var obj = inData[key];
          if (
            (!window.plugin.pnav.settings.lat ||
              !window.plugin.pnav.settings.lng ||
              !window.plugin.pnav.settings.radius ||
              checkDistance(obj.lat,obj.lng,window.plugin.pnav.settings.lat,window.plugin.pnav.settings.lng) <= window.plugin.pnav.settings.radius) &&
            (!done || !done.includes(inData[key]))
          ) {
            data.push(obj);
          }
        });
        localStorage.setItem(`plugin-pnav-todo-${  type}`, JSON.stringify(data));
      }
      var i = 0;
      var wait = 2000; // Discord WebHook accepts 30 Messages in 60 Seconds.
      window.onbeforeunload = function () {
        if (i && data && type && i < data.length) {
          localStorage.setItem(
            `plugin-pnav-todo-${  type}`,
            JSON.stringify(data.slice(i))
          );
          var done = localStorage.getItem(`plugin-pnav-done-${  type}`)
            ? JSON.parse(localStorage.getItem(`plugin-pnav-done-${  type}`))
            : null;
          if (!done) {
            localStorage.setItem(
              `plugin-pnav-done-${  type}`,
              JSON.stringify(data.slice(0, i))
            );
          } else {
            done.concat(data.slice(0, i));
            localStorage.setItem(
              `plugin-pnav-done-${  type}`,
              JSON.stringify(done)
            );
          }
        }
        return null;
      };
      var doit = function () {
        var done = localStorage.getItem(`plugin-pnav-done-${  type}`)
          ? JSON.parse(localStorage.getItem(`plugin-pnav-done-${  type}`))
          : null;
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
          if (window.plugin.pnav.abort) {
            let todo = data.slice(i);
            localStorage.setItem(
              `plugin-pnav-todo-${  type}`,
              JSON.stringify(todo)
            );
            if (!done) {
              localStorage.setItem(
                `plugin-pnav-done-${  type}`,
                JSON.stringify(data.slice(0, i))
              );
            } else {
              done.concat(data.slice(0, i));
              localStorage.setItem(
                `plugin-pnav-done-${  type}`,
                JSON.stringify(done)
              );
            }
            window.plugin.pnav.abort = false;
            window.plugin.pnav.wip = false;
          } else {
            if (i % 10 == 0) {
              // sometimes save the state in case someone exits IITC Mobile without using the Back Button
              let todo = data.slice(i);
              localStorage.setItem(
                `plugin-pnav-todo-${  type}`,
                JSON.stringify(todo)
              );
              if (!done) {
                localStorage.setItem(
                  `plugin-pnav-done-${  type}`,
                  JSON.stringify(data.slice(0, i))
                );
              } else {
                done.concat(data.slice(0, i));
                localStorage.setItem(
                  `plugin-pnav-done-${  type}`,
                  JSON.stringify(done)
                );
              }
            }
            var entry = data[i];
            let lat = entry.lat;
            let lng = entry.lng;
            let name = entry.name
              .replaceAll('\\', '\\\\')
              .replaceAll('"', '\\"'); // escaping Backslashes and Hyphens in Portal Names
            let prefix = window.plugin.pnav.settings.prefix;
            let ex = entry.isEx ? true : false;
            let options = ex ? ' "ex_eligible: 1"' : '';
            let request = window.plugin.pnav.request;
            var params = {
              username: window.plugin.pnav.settings.name,
              avatar_url: '',
              content: `${prefix}create poi ${type} "${name}" ${lat} ${lng}${options}`,
            };
            request.open('POST', window.plugin.pnav.settings.webhookUrl);
            request.setRequestHeader('Content-type', 'application/json');
            request.onload = function () {
              if (request.status == 204 || request.status == 200) {
                i++;
                setTimeout(doit, wait);
              } else {
                console.log(`status code ${  request.status}`);
                setTimeout(doit, 3 * wait);
              }
            };
            request.onerror = function () {
              setTimeout(doit, 3 * wait);
            };
            request.send(JSON.stringify(params), false);
            // console.log('$create poi ' + type + ' "' + name + '" ' + lat + ' ' + lng + options);
          }
        } else {
          $('#exportState').text('Export Ready!');
          okayButton.text('OK');
          okayButton.prop('title', '');
          window.plugin.pnav.wip = false;
          localStorage.removeItem(`plugin-pnav-todo-${  type}`);
          if (!done) {
            localStorage.setItem(
              `plugin-pnav-done-${  type}`,
              JSON.stringify(data.slice(0, i))
            );
          } else {
            done.concat(data.slice(0, i));
            localStorage.setItem(
              `plugin-pnav-done-${  type}`,
              JSON.stringify(done)
            );
          }
          window.onbeforeunload = null;
        }
      };

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
              <label id="exportNumber">0</label><label> of ${data.length}</label>
              <br>
              <label>Time remaining: </label>
              <label id="exportTimeRemaining">${Math.round((wait * data.length) / 1000)}</label>
              <label>s</label>
        `,
        width: 'auto',
        title: 'PokeNav Bulk Export Progress',
      });

      // console.log(dialog);

      let thisDialog = $('.ui-dialog').has('#dialog-bulkExportProgress')[0];
      // console.log(thisDialog);
      var okayButton = $('.ui-button', thisDialog).not(
        '.ui-dialog-titlebar-button'
      );
      okayButton.text('Pause');
      okayButton.prop(
        'title',
        'store Progress locally and stop Exporting. If you wish to restart, go to Settings and click the Export Button again.'
      );
      okayButton.on('click', function () {
        window.plugin.pnav.abort = true;
      });

      $('.ui-button.ui-dialog-titlebar-button-close', thisDialog).on(
        'click',
        function () {
          window.plugin.pnav.abort = true;
        }
      );

      doit();
    } else {
      console.log('Bulk Export already running!');
    }
  }
  /*
  the idea of the following funtion was taken from https://stackoverflow.com/a/14561433
  by User talkol (https://stackoverflow.com/users/1025458/talkol).
  The License is CC BY-SA 4.0 (https://creativecommons.org/licenses/by-sa/4.0/)
  The Code was slightly adapted.
  */
  function checkDistance(lat1, lon1, lat2, lon2) {
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

  function copyfieldvalue(id) {
    var field = document.getElementById(id);
    field.focus();
    field.setSelectionRange(0, field.value.length);
    field.select();
    var copysuccess = copySelectionText();
  }

  function copySelectionText() {
    var copysuccess;
    try {
      copysuccess = document.execCommand('copy');
    } catch (e) {
      copysuccess = false;
    }
    return copysuccess;
  }

  // source: Oscar Zanota on Dev.to (https://dev.to/oskarcodes/send-automated-discord-messages-through-webhooks-using-javascript-1p01)
  function sendMessage(msg) {
    let request = window.plugin.pnav.request;
    var params = {
      username: window.plugin.pnav.settings.name,
      avatar_url: '',
      content: msg,
    };
    request.open('POST', window.plugin.pnav.settings.webhookUrl);
    request.setRequestHeader('Content-type', 'application/json');
    request.send(JSON.stringify(params), false);
  }

  var setup = function () {
    console.log('azaza');
    if (localStorage['plugin-pnav-settings']) {
      window.plugin.pnav.settings = JSON.parse(
        localStorage.getItem('plugin-pnav-settings')
      );
    }
    $('#toolbox').append(
      '<a title="Configure PokeNav" style="margin-left:4px" onclick="if(window.plugin.pnav.wip==false){window.plugin.pnav.showSettings();}return false;" accesskey="s">PokeNav Settings</a>'
    );
    $('body').prepend(
      '<input id="copyInput" style="position: absolute;"></input>'
    );
    window.addHook('portalSelected', function (data) {
      console.log(data);
      var guid = data.selectedPortalGuid;
      window.plugin.pnav.selectedGuid = guid;
      setTimeout(function () {
        if ($('.PogoButtons').length == 0) {
          $('#portaldetails').append(`
          <div id="PNav" style="color:#fff;display:flex">
          <Label><input type="radio" checked="true" name="type" value="stop" id="PNavStop"/>Stop</label>
          <Label><input type="radio" name="type" value="gym" id="PNavGym"/>Gym</label>
          <Label><input type="radio" name="type" value="ex" id="PNavEx"/>Ex Gym</label>
          <a style="margin-left:auto;margin-right:5px" title="Copy the PokeNav Command to Clipboard or post to Discord via WebHook" onclick="window.plugin.pnav.copy();return false;" accesskey="c">Copy PokeNav</a>
          </div>
        `);
        } else {
          $('.PogoButtons').append(`
        <a style="margin-left:auto;margin-right:5px" title="Copy the PokeNav Command to Clipboard or post to Discord via WebHook" onclick="window.plugin.pnav.copy();return false;" accesskey="c">Copy PokeNav</a>
        `);
          $('.PogoButtons').css('display', 'flex');
          $('.PogoButtons').css('align-items', 'baseline');
        }
      }, 5);
    });
  };

  // PLUGIN END //////////////////////////////////////////////////////////

  setup.info = plugin_info; // add the script info data to the function as a property
  if (!window.bootPlugins) window.bootPlugins = [];
  window.bootPlugins.push(setup);
  // if IITC has already booted, immediately run the 'setup' function
  if (window.iitcLoaded && typeof setup === 'function') setup();
} // wrapper end
// inject code into site context
var script = document.createElement('script');
var info = {};
if (typeof GM_info !== 'undefined' && GM_info && GM_info.script)
  info.script = {
    version: GM_info.script.version,
    name: GM_info.script.name,
    description: GM_info.script.description,
  };
script.appendChild(
  document.createTextNode(`(${  wrapper  })(${  JSON.stringify(info)  });`)
);
(document.body || document.head || document.documentElement).appendChild(
  script
);
