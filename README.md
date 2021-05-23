# IITC-Plugin: Copy PokeNav Command
IITC Plugin that copies Portal Info to Clipboard or sends it directly to Discord via WebHook in the format needed by the PokeNav Discord Bot as follows:

```@PokeNav create poi <type> "<name>" <latitude> <longitude> "ex-eligible: 1" (if Ex Gym)```


## Prerequisites
To use this IITC Plugin, you need
- An Account for Niantic's game Ingress
- If you want to use your Computer (recommended), you need a Userscript Manager for your Browser (e.g. [Tampermonkey](https://www.tampermonkey.net/)) and a Version of IITC installed ([IITC](https://iitc.me/desktop/) or [IITC-CE](https://iitc.app/download_desktop.html)). Alternatively you can use the Browser Addon "IITC Button" available for [Chrome](https://chrome.google.com/webstore/detail/iitc-button/febaefghpimpenpigafpolgljcfkeakn) and [Firefox](https://addons.mozilla.org/de/firefox/addon/iitc-button/).
- If you want to use your Smartphone instead, you have to install IITC Mobile ([Play Store](https://play.google.com/store/apps/details?id=com.cradle.iitc_mobile), [GitLab](https://gitlab.com/ruslan.levitskiy/iitc-mobile)) or IITC CE Mobile ([Play Store](https://play.google.com/store/apps/details?id=org.exarhteam.iitc_mobile), [GitHub](https://github.com/IITC-CE/ingress-intel-total-conversion)) for Android. For IPhone you can only use IITC Mobile (not tested with this Plugin!) ([App Store](https://apps.apple.com/app/iitc-mobile/id1032695947), [GitHub](https://github.com/HubertZhang/IITC-Mobile)).


## Installation
To install the Plugin, click **[here](https://raw.github.com/MaxEtMoritz/PNavCopy/main/PNavCopy.user.js)**.

You should be asked if you want to install an external Plugin. Confirm the Installation and you are done!


## Features
With This Plugin you can...
- Classify a portal manually as Stop, Gym or EX Gym or use the Info already collected with Pogo Tools (see [Integrations section](#integrations))
- Copy The Command to Clipboard, use a WebHook to send it directly to the appropriate Discord channel or use the [Companion Bot](#about-the-companion-bot)
- Send all the Data already collected with PoGo Tools to PokeNav with a few Clicks
- Pause the Bulk export and start off where you ended it
- Check for modifications of Pogo Tools data automatically
- Send or copy modification or deletion Commands for PokeNav, or let the [Companion Bot](#about-the-companion-bot) do the work.
- View your PokeNav community bounds as a circle on the map
- Represent the state of the export in Colors with a highlighter: PokeStops are blue, Gyms red and Ex raid gyms have a red border and yellow filling.

## Integrations
If you use the [Pogo tools plugin by AlfonsoML](https://gitlab.com/AlfonsoML/pogo-s2/), the info entered there is used to determine Type and Ex Eligibility if applicable. Otherwise you can choose manually.

If you use the Plugin, you also have the option to upload all gathered Data at once.

## Original Source
The Plugin is based on a plugin included in a [Fork of the original IITC Mobile App](https://gitlab.com/ruslan.levitskiy/iitc-mobile) ([direct link to the Plugin](https://gitlab.com/ruslan.levitskiy/iitc-mobile/-/blob/master/app/src/main/assets/plugins/portal-link-copier.user.js)).

## Note
The Plugin is not the very best code style and the code may not be very "error-friendly" because i am in no way an expert in JavaScript at the moment, but the important thing for me was to get it work, and it does exacly that, nothing more :wink:

## WebHook How-to:
A Tutorial on how to set up a WebHook in Discord can be found [here](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks).

The WebHook has to be set up for the PokeNav Admin Channel (named #pokenav by default).

If you created the WebHook, copy the WebHook URL and paste it into the Text Box in the Settings Dialog of the Userscript. The URL will be stored in Local Browser Storage for you, so you normally won't have to re-enter it.

__Note:__ Have in mind that anyone who has the WebHook URL and knows how to post to WebHooks can send any Message he likes to the Channel, so be cautious who you give the WebHook URL to.

## About the Companion Bot
The Companion Bot is a helper Bot that recieves a JSON file from the WebHook containing all PoI to create / update and posts the PokeNav commands one at a time. This is because WebHooks can only post 30 Messages per minute, resulting in long waiting times if you want to create all PoI via WebHook. And you have to keep IITC on all the time.

After the Export, the Bot does its Work automatically without the need to keep IITC open so long.

__Note:__ The Companion Bot is still under construction and i don't know yet where to host it. If you want to try it, open an Issue. Eventually i will make it go Online then and share the Invite Link.

## How to contribute?
You can contribute by...
- translating this Plugin into your native language. A guide on how to translate can be found [here](/Translating.md).
- contributing Code to the plugin. Please fork this repository and open a pull request. I will then take a look at it and if i consider it good, i'll merge it into the test branch and later on into main if everything is working.
- reporting Bugs and other issues.