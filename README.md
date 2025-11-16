# IITC-Plugin: Copy PokeNav Command

[![Join the Discord server!](https://discord.com/api/guilds/781826303128371241/widget.png?style=banner4)](https://discord.gg/j7ZZ2N8r73 "Join the Discord server!")


IITC Plugin that copies Portal Info to Clipboard or sends it directly to Discord via WebHook in the format needed by the PokeNav Discord Bot as follows:

```@PokeNav create poi <type> «<name>» <latitude> <longitude> "ex-eligible: 1" (if Ex Gym)```


## Prerequisites
To use this IITC Plugin, you need
- An Account for Niantic's game Ingress
- If you want to use your Computer (recommended), you need a Userscript Manager for your Browser (e.g. [Tampermonkey](https://www.tampermonkey.net/)) and a Version of IITC installed ([IITC](https://iitc.me/desktop/) or [IITC-CE](https://iitc.app/download_desktop.html)). Alternatively you can use the Browser Addon "IITC Button" available for [Chrome](https://chrome.google.com/webstore/detail/iitc-button/febaefghpimpenpigafpolgljcfkeakn) and [Firefox](https://addons.mozilla.org/de/firefox/addon/iitc-button/).
- If you want to use your Smartphone instead, you have to install IITC Mobile ([Play Store](https://play.google.com/store/apps/details?id=com.cradle.iitc_mobile), [GitLab](https://gitlab.com/ruslan.levitskiy/iitc-mobile)) or IITC CE Mobile ([Play Store](https://play.google.com/store/apps/details?id=org.exarhteam.iitc_mobile), [GitHub](https://github.com/IITC-CE/ingress-intel-total-conversion)) for Android. For IPhone you can only use IITC Mobile (not tested with this Plugin!) ([App Store](https://apps.apple.com/app/iitc-mobile/id1032695947), [GitHub](https://github.com/HubertZhang/IITC-Mobile)).


## Installation
To install the Plugin, click :point_right:**[here](https://raw.github.com/MaxEtMoritz/PNavCopy/main/PNavCopy.user.js)**:point_left:.

You should be asked if you want to install an external Plugin. Confirm the Installation and you are done!


## Features
With This Plugin you can...
- Classify a portal manually as Stop, Gym or EX Gym or use the Info already collected with Pogo Tools (see [Integrations section](#integrations))
- Copy The Command to Clipboard, use a WebHook to send it directly to the appropriate Discord channel ~~or use the [Companion Bot](#about-the-companion-bot)~~
- Decide if the commands should be sent directly to the Discord channel or a thread
- Send all the Data already collected with PoGo Tools to PokeNav with a few Clicks
- Pause the Bulk export and start off where you ended it
- Check for modifications of Pogo Tools data automatically
- Send or copy modification or deletion Commands for PokeNav, ~~or let the [Companion Bot](#about-the-companion-bot) do the work~~.
- View your PokeNav community bounds as a circle on the map
- Represent the state of the export in Colors with a highlighter: PokeStops are blue, Gyms red and Elite raid gyms have a red border and yellow filling.

## Integrations
If you use the [Pogo tools plugin by AlfonsoML](https://github.com/AlfonsoML-s/pogo-s2) (development from original author stalled, but still working), the info entered there is used to determine Type and Ex Eligibility if applicable. Otherwise you can choose manually.

If you use the Plugin, you also have the option to upload all gathered Data at once.

:warning: Attention! :warning:
There exists an actively maintained fork of the original plug-in by [NvlblNm](https://gitlab.com/NvlblNm/pogo-s2). But this fork is INCOMPATIBLE with my plug-in, because the way the data is stored was fundamentally changed in this fork.
So please stick to the version linked on this page.

## Original Source / Dependencies
Credits for the original source and licenses of dependencies can be found [here](/dependency-credits.md).

## Note
The Plugin is not the very best code style and the code may not be very "error-friendly" because i am in no way an expert in JavaScript at the moment, but the important thing for me was to get it work, and it does exacly that, nothing more :wink:

## WebHook How-to:
A Tutorial on how to set up a WebHook in Discord can be found [here](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks).

The WebHook has to be set up for the PokeNav Admin Channel (named #pokenav by default).

If you created the WebHook, copy the WebHook URL and paste it into the Text Box in the Settings Dialog of the Userscript. The URL will be stored in Local Browser Storage for you, so you normally won't have to re-enter it.

__Note:__ Have in mind that anyone who has the WebHook URL and knows how to post to WebHooks can send any Message he likes to the Channel, so be cautious who you give the WebHook URL to.

## About the Companion Bot
:warning: **The Companion Bot has been taken offline for several reasons.** :warning: <br/>
The source code is still available. If you plan to host the bot yourself, inform me and i will try to assist in the hosting process.

The Companion Bot was a helper Bot that recieved a JSON file from the WebHook or an arbitrary CSV file from the user containing all PoI to create / update and posted the PokeNav commands one at a time. This is because WebHooks can only post 30 Messages per minute, resulting in long waiting times if you want to create all PoI via WebHook. And you have to keep IITC on all the time.

After the export, the Bot did its work automatically without the need to keep IITC open so long.

## How to contribute?
You can contribute by...
- translating this Plugin into your native language. A guide on how to translate can be found [here](/Translating.md).
- contributing Code to the plugin or the bot. Please fork this repository and open a pull request. I will then take a look at it and if i consider it good, i'll merge it into the dev branch and later on into main if everything is working.
- hosting the companion bot.
- reporting Bugs and other issues.
