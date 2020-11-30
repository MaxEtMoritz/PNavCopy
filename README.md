# IITC-Plugin: Copy PokeNav Creation Command
IITC Plugin that copies Portal Info to Clipboard or sends them directly to Discord via WebHook in the format needed by the PokeNav Discord Bot as follows:

```$create poi <type> "<name>" <location> "ex-eligibility: 1" (if Ex Gym)```


### Features
- Classify a portal manually as Stop, Gym or EX Gym or use the Info already collected with Pogo Tools (see Integrations section)
- Copy The Command to Clipboard or use a WebHook to send it directly to the appropriate Discord channel.
- Send all the Data already collected with PoGo Tools to PokeNav with a few Clicks.

### Integrations
If you use the [Pogo tools plugin by AlfonsoML](https://gitlab.com/AlfonsoML/pogo-s2/), the info entered there is used to determine Type and Ex Eligibility if applicable. Otherwise you can choose manually.

If you use the Plugin, you also have the option to upload all gathered Data at once.

### Original Source
The Plugin is based on a plugin included in a [Fork of the original IITC Mobile App](https://gitlab.com/ruslan.levitskiy/iitc-mobile) ([direct link to the Plugin](https://gitlab.com/ruslan.levitskiy/iitc-mobile/-/blob/master/app/src/main/assets/plugins/portal-link-copier.user.js))

### Note
The Plugin is not the very best code (and visual) style and the code may not be very "error-friendly" because i am in no way an expert in JavaScript at the moment, but the important thing for me was to get it work, and it does exacly that, nothing more :-)

## Web Hook How-to:
A Tutorial on how to set up a Web Hook in Discord can be found [here](https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks).

The WebHook has to be set up for the PokeNav Admin Channel (named #pokenav by default).

If you created the Web Hook, copy the Web Hook URL and paste it into the Text Box in the Settings Dialog of the Userscript. The URL will be stored in Local Browser Storage for you, so you normally won't have to re-enter it.

__Note:__ Have in mind that anyone who has the Web Hook URL and knows how to post to Web Hooks can send any Message he likes to the Channel, so be cautious who you give the Web Hook URL to.
__Second Note:__ Some Bots ignore Messages sent via Web Hooks or other Bots, but the PokeNav Bot doesn't at the moment (PokeNav v 1.89.2). No Guarantee that this will stay like that forever...