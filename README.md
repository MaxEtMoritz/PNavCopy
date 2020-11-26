# IITC-Plugin: Copy PokeNav Creation Command
IITC Plugin that copies Portal Info to Clipboard in the format needed by the PokeNav Discord Bot as follows:

```$create poi <type> "<name>" <location> <"ex-eligibility: 1" if ex gym>```

If you use the [Pogo tools plugin by AlfonsoML](https://gitlab.com/AlfonsoML/pogo-s2/), the Info entered there is used to determine Type and Ex Eligibility if applicable. Otherwise you can choose manually.

The Plugin is based on a plugin included in a [Fork of the original IITC Mobile App](https://gitlab.com/ruslan.levitskiy/iitc-mobile) ([direct link to the Plugin](https://gitlab.com/ruslan.levitskiy/iitc-mobile/-/blob/master/app/src/main/assets/plugins/portal-link-copier.user.js))

**Note**: Alias and Note options are not supported. If you need one of them, add them manually before sending the message to the Bot!

**Note**: The Plugin is not the very best code (and visual) style and the code may not be very "error-friendly" because i am in no way an expert in JavaScript at the moment, but the important thing for me was to get it work, and it does exacly that, nothing more :-)