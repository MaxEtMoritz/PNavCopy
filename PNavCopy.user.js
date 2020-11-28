// ==UserScript==
/* globals jQuery, $, waitForKeyElements */
// @id             pnavcopy@maxetmoritz
// @name           IITC plugin: Copy PokeNav Creation Command
// @category       Misc
// @downloadURL    https://github.com/MaxEtMoritz/PNavCopy/releases/download/latest/PNavCopy.user.js
// @author         MaxEtMoritz
// @version        1.2
// @namespace      https://github.com/MaxEtMoritz/PNavCopy
// @description    Copy portal info in PokeNav Discord bot command format.
// @include        http*://intel.ingress.com/*
// @grant          none
// ==/UserScript==

//original Plug-In is from https://gitlab.com/ruslan.levitskiy/iitc-mobile/-/tree/master, the License of this project is provided below:

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
    if(typeof window.plugin !== 'function') window.plugin = function() {};

// PLUGIN START ////////////////////////////////////////////////////////

// use own namespace for plugin
    window.plugin.pnav = function() {};
    window.plugin.pnav.selectedGuid = null;
    window.plugin.pnav.webhookURL = "";
    window.plugin.pnav.request = new XMLHttpRequest();
    window.plugin.pnav.copy = function() {
        var input = $('#copyInput');
        if(window.selectedPortal){
            input.show();
            var portal = window.portals[window.plugin.pnav.selectedGuid];
            var name = portal.options.data.title;
            var latlng = portal.getLatLng();
            var lat = latlng.lat;
            var lng = latlng.lng;
            var opt = " ";
            var type = "";
            if(window.plugin.pogo){
                if($('.pogoStop span').css('background-position') == "100% 0%"){
                    type = "stop";
                }
                else if($('.pogoGym span').css('background-position') == "100% 0%"){
                    type = "gym";
                    if($('#PogoGymEx').prop('checked')==true){
                        opt += "\"ex_eligible: 1\"";
                    }
                }
            }
            else{
                if(document.getElementById('PNavEx').checked){
                    type = "gym";
                    opt += "\"ex_eligible: 1\"";
                }
                else if (document.getElementById('PNavGym').checked){
                    type = "gym";
                }
                else{
                    type = "stop";
                }
            }
            if($('#PNavSponsored').prop('checked')==true){
                opt += " \"sponsored: 1\"";
            }
            input.val("$create poi " + type + " \"" + name + "\" " + lat + ' ' + lng + opt);
            if(window.plugin.pnav.webhookURL != ""){
                sendMessage("$create poi " + type + " \"" + name + "\" " + lat + ' ' + lng + opt);
                console.log('sent!');
            }
            else{
                copyfieldvalue('copyInput');
            }
            input.hide();
        }
    };

    window.plugin.pnav.showSettings = function(){
        let validURL = "^https?://discord(app)?.com/api/webhooks/[0-9]*/.*";
        var html = `
        <label title="Paste the URL of the WebHook you created in your Server to issue Location Commands to the PokeNav Bot Here. If left blank, the Commands are copied to clipboard.">
            Discord WebHook URL:
            <input type="text" id="pnavhookurl" value="` + window.plugin.pnav.webhookURL + `" pattern="` + validURL + `"/>
        </label>
        `;
        if(window.plugin.pogo){
            html += `
            <p><a title="Grab the File where all Gyms are stored by PoGoTools and send them one by one via Web Hook. This can take much time!" onclick="window.plugin.pnav.bulkExportGyms();return false;">Export all PogoTools Gyms</a></p>
            <p><a title="Grab the File where all Stops are stored by PoGoTools and send them one by one via Web Hook. This can take much time!" onclick="window.plugin.pnav.bulkExportStops();return false;">Export all PogoTools Stops</a></p>
            `;
        }
        const container = dialog({
            id:'pnavsettings',
            width: 'auto',
            html: html,
            title: 'PokeNav Settings'
        });
        $('#pnavhookurl').on('input', function(){
            if(new RegExp(validURL).test($(this).val())){
                window.plugin.pnav.webhookURL = $(this).val();
                localStorage.setItem('plugin-pnav-settings', window.plugin.pnav.webhookURL);
            }
        });
    };

    window.plugin.pnav.bulkExportGyms = function(){
        var data;
        if(localStorage['plugin-pogo']){
            data = JSON.parse(localStorage.getItem('plugin-pogo'));
        }
        else if(localStorage['plugin-pogo-data']){
            data = JSON.parse(localStorage.getItem('plugin-pogo-data'));
        }
        else{
            alert("Pogo Tools is loaded but no Data File was found!");
        }
        if(data && data.gyms){
            bulkExport(data.gyms,'gym');
        }
    };

    window.plugin.pnav.bulkExportStops = function(){
        var data;
        if(localStorage['plugin-pogo']){
            data = JSON.parse(localStorage.getItem('plugin-pogo'));
        }
        else if(localStorage['plugin-pogo-data']){
            data = JSON.parse(localStorage.getItem('plugin-pogo-data'));
        }
        else{
            alert("Pogo Tools is loaded but no Data File was found!");
        }
        if(data && data.stops){
            bulkExport(data.stops,'stop');
        }
    };

    function bulkExport(data, type){
        //console.log(data);
        var keys = Object.keys(data);
        var i = 0;
        var wait = 1000;
        var rateLimit = wait;
        //TODO test and add a way to abort that stuff!
        var doit = function(){
             if($('#exportProgressBar')){
                 $('#exportProgressBar').val(i);
             }
            if($('#exportNumber')){
                 $('#exportNumber').text(i);
             }
            if($('#exportTimeRemaining')){
                 $('#exportTimeRemaining').text(Math.round((wait * (keys.length - i)) / 1000));
             }
            if(i<keys.length){
                var entry = data[keys[i]];
                let lat = entry.lat;
                let lng = entry.lng;
                let name = entry.name;
                let ex = entry.isEx?true:false;
                let options = ex?' "ex_eligible: 1"':''
                let request = window.plugin.pnav.request;
                var params = {
                    username: window.PLAYER.nickname,
                    avatar_url: "",
                    content: '$create poi ' + type + ' "' + name + '" ' + lat + ' ' + lng + options
                };
                request.open("POST", window.plugin.pnav.webhookURL);
                request.setRequestHeader('Content-type', 'application/json');
                request.onload = function(){
                    if(request.status==204){
                    let limit = request.getResponseHeader('X-RateLimit-Limit'); //The number of requests that can be made
                    let remaining = request.getResponseHeader('X-RateLimit-Remaining'); //The number of remaining requests that can be made
                    let reset = request.getResponseHeader('X-RateLimit-Reset'); //Epoch time (seconds since 00:00:00 UTC on January 1, 1970) at which the rate limit resets
                    let bucket = request.getResponseHeader('X-RateLimit-Bucket'); //A unique string denoting the rate limit being encountered (non-inclusive of major parameters in the route path)
                    let resetAfter = request.getResponseHeader('X-RateLimit-Reset-After'); //Total time (in seconds) of when the current rate limit bucket will reset. Can have decimals to match previous millisecond ratelimit precision
                    if(remaining&&remaining<=2){
                        rateLimit = 1000 * resetAfter;
                    }
                    console.log('rate limit is near!')
                    }
                    else if(request.status==200&&rateLimit>wait){
                        rateLimit = wait;
                    }
                    else if(request.status==429){
                        console.log(429);
                    }
                }
                request.send(JSON.stringify(params), false);
                //console.log('$create poi ' + type + ' "' + name + '" ' + lat + ' ' + lng + options);
                i++;
                setTimeout(doit,Math.max(wait,rateLimit));
            }
            else{

            }
        }

        setTimeout(doit,Math.max(wait,rateLimit));

        var dialog = window.dialog({
            id: 'bulkExportProgress',
            html: `
                <label>Exporting...</label>
                <p>
                    <label>
                        Progress:
                        <progress id="exportProgressBar" value="0" max=` + keys.length + `/>
                    </label>
                </p>
                <label id="exportNumber">0</label><label> of ` + keys.length +`</label>
                <br>
                <label>Time remaining: </label><label id="exportTimeRemaining">` + Math.round((wait * keys.length) / 1000) + `</label><label>s</label>
            `,
            // TODO time remaining does not take the rate limit into account!
            width: 'auto',
            title: 'PokeNav Bulk Export Progress',
            buttons: {
                Abort: function(){
                    clearInterval(timer);
                    dialog.dialog('close');
                }
            }
        });
    }

    function copyfieldvalue(id){
        var field = document.getElementById(id);
        field.focus();
        field.setSelectionRange(0, field.value.length);
        field.select();
        var copysuccess = copySelectionText();
    }

    function copySelectionText(){
        var copysuccess;
        try{
            copysuccess = document.execCommand("copy");
        } catch(e){
            copysuccess = false;
        }
        return copysuccess;
    }

    //source: https://dev.to/oskarcodes/send-automated-discord-messages-through-webhooks-using-javascript-1p01
    function sendMessage(msg){
       let request = window.plugin.pnav.request;
       var params = {
            username: window.PLAYER.nickname,
            avatar_url: "",
            content: msg
        };
        request.open("POST", window.plugin.pnav.webhookURL);
        request.setRequestHeader('Content-type', 'application/json');
        request.send(JSON.stringify(params), false);
    }

    var setup = function() {
        console.log('azaza');
        if(localStorage['plugin-pnav-settings']){
            window.plugin.pnav.webhookURL = localStorage.getItem('plugin-pnav-settings');
        }
        if(window.plugin.pogo){
            $('#toolbox').append('<input type="checkbox" name="sponsored" id="PNavSponsored"><label for="PNavSponsored">Sponsored</label><a title="Copy the PokeNav Command to Clipboard or post to Discord via Web Hook" onclick="window.plugin.pnav.copy();return false;" accesskey="c">Copy PokeNav</a>');
        }
        else{
            $('#toolbox').append('<input type="radio" checked="true" name="type" value="stop" id="PNavStop"/><Label for="PNavStop">Stop</label><input type="radio" name="type" value="gym" id="PNavGym"/><Label for="PNavGym">Gym</label><input type="radio" name="type" value="ex" id="PNavEx"/><Label for="PNavEx">Ex Gym</label><input type="checkbox" name="sponsored" id="PNavSponsored"><label for="PNavSponsored">Sponsored</label><a title="Copy the PokeNav Command to Clipboard or post to Discord via Web Hook" onclick="window.plugin.pnav.copy();return false;" accesskey="c">Copy PokeNav</a>');
        }
        $('#toolbox').append('<a title="Configure PokeNav" onclick="window.plugin.pnav.showSettings();return false;" accesskey="s">PokeNav Settings</a>')
        $('body').prepend('<input id="copyInput" style="position: absolute;"></input>');
        window.addHook('portalSelected', function(data){
            console.log(data);
            var guid = data.selectedPortalGuid;
            window.plugin.pnav.selectedGuid = guid;
        });
    };

// PLUGIN END //////////////////////////////////////////////////////////

    setup.info = plugin_info; //add the script info data to the function as a property
    if(!window.bootPlugins) window.bootPlugins = [];
    window.bootPlugins.push(setup);
// if IITC has already booted, immediately run the 'setup' function
    if(window.iitcLoaded && typeof setup === 'function') setup();
} // wrapper end
// inject code into site context
var script = document.createElement('script');
var info = {};
if (typeof GM_info !== 'undefined' && GM_info && GM_info.script) info.script = { version: GM_info.script.version, name: GM_info.script.name, description: GM_info.script.description };
script.appendChild(document.createTextNode('('+ wrapper +')('+JSON.stringify(info)+');'));
(document.body || document.head || document.documentElement).appendChild(script);
