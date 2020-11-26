// ==UserScript==
/* globals jQuery, $, waitForKeyElements */
// @id             pnav@nbg
// @name           IITC plugin: Copy PokeNav Creation Command
// @category       Misc
// @author         MaxEtMoritz
// @version        1.0
// @namespace      https://github.com/jonatkins/ingress-intel-total-conversion
// @description    Copy portal info in PokeNav command format: $create poi <type> "<Name>" <location> <ex eligibility>
// @include        https://intel.ingress.com/*
// @include        http://intel.ingress.com/*
// @match          https://intel.ingress.com/*
// @match          http://intel.ingress.com/*
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
    window.plugin.pnav.copy = function() {
        var input = $('#copyInput');
        if(window.selectedPortal){
            input.show();
            var portal = window.portals[window.plugin.pnav.selectedGuid];
            var name = portal.options.data.title;
            var latlng = portal.getLatLng();
            var lat = latlng.lat;
            var lng = latlng.lng;
            var opt = "";
            var type = "";
            if(window.plugin.pogo){
                if($('.pogoStop span').css('background-position') == "100% 0%"){
                    type = "stop";
                }
                else if($('.pogoGym span').css('background-position') == "100% 0%"){
                    type = "gym";
                    if($('#PogoGymEx').prop('checked')==true){
                        opt = "\"ex_eligible: 1\"";
                    }
                }
            }
            else{
                if(document.getElementById('PNavEx').checked){
                    type = "gym";
                    opt = "\"ex_eligible: 1\"";
                }
                else if (document.getElementById('PNavGym').checked){
                    type = "gym";
                }
                else{
                    type = "stop";
                }
            }
            input.val("$create poi " + type + " \"" + name + "\" " + lat + '-' + lng + " " + opt);
            copyfieldvalue('copyInput');
            input.hide();
        }
    };

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


    var setup = function() {
        console.log('azaza');
        if(window.plugin.pogo){
            $('#toolbox').append('<div class="PNavCpy"><a onclick="window.plugin.pnav.copy();return false;" accesskey="c">Copy PokeNav</a></div>');
        }
        else{
            $('#toolbox').append('<div class="PNavCpy"><input type="radio" checked="true" name="type" value="stop" id="PNavStop"/><Label for="PNavStop">Stop</label><input type="radio" name="type" value="gym" id="PNavGym"/><Label for="PNavGym">Gym</label><input type="radio" name="type" value="ex" id="PNavEx"/><Label for="PNavEx">Ex Gym</label><a onclick="window.plugin.pnav.copy();return false;" accesskey="c">Copy PokeNav</a></div>');
        }
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


