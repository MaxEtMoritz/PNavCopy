# IITC-Plugin: Copy PokeNav Creation Command
IITC Plugin that copies Portal Info to Clipboard in the format needed by the PokeNav Discord Bot as follows:

```$create poi <type> "<name>" <location> <options>```

### Supported Options:
- ```"ex-eligibility: 1"```
- ```"sponsored: 1"```

### Integrations
If you use the [Pogo tools plugin by AlfonsoML](https://gitlab.com/AlfonsoML/pogo-s2/), the info entered there is used to determine Type and Ex Eligibility if applicable. Otherwise you can choose manually.

### Original Source
The Plugin is based on a plugin included in a [Fork of the original IITC Mobile App](https://gitlab.com/ruslan.levitskiy/iitc-mobile) ([direct link to the Plugin](https://gitlab.com/ruslan.levitskiy/iitc-mobile/-/blob/master/app/src/main/assets/plugins/portal-link-copier.user.js))

### Note
The Plugin is not the very best code (and visual) style and the code may not be very "error-friendly" because i am in no way an expert in JavaScript at the moment, but the important thing for me was to get it work, and it does exacly that, nothing more :-)