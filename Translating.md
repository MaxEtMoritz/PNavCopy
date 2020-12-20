# How to Translate:
At the beginning of the Userscript you can find all strings that need to be translated. If you want to translate in a language, just copy all english strings and paste them below, giving a new language code (like en, de, ru etc., currently not something like en-US etc.).
# Translation Syntax:
Each string has a key that is the identifier of the string. Don't change it, otherwise the translated string will not be found and the English string will be displayed!

The Values to the Keys can be simple strings like ```'Hello World!'``` or they can be in a structure called 'nested strings' by me:
# 'Nested string' Syntax:
```javascript
{
    ...
    foo: ['This is', ' a string consisting', ' of three parts.'],
    ...
}
```
You can create Strings from different parts if You need to. The given example does not make much sense, but this is especially useful if you want to use more features of 'nested strings' in a single string.

```javascript
{
    ...
    foo: {
        temperature: {
            hot: 'it is hot right now!',
            cold: 'it is cold right now!'
        }
    },
    ...
}
```
You can give your translation more variability by using the decision syntax: If a string is needed, Options may be given to better decide how the string will look.

The options are an object with key/value pairs. At the moment only the option 'send' is used that specifies if a WebHook url is specified what implies that the Commands should be sent to PokeNav via WebHook rather than being copied to clipboard.

The 'outer' key has to match the key of the option, the 'inner' keys have to match the values of that option key. The values of the 'inner' keys are the strings that will be returned if the option has the respective value.

A special 'inner' key is ```default```: here you can specify a default string that is returned if the option value did not match one of the 'inner' keys or if the option was somehow missing. if no default is specified, the value of the first key of the inner object is returned.

Only one outer key per object is supported!

```javascript
{
    ...
    foo: ['This is a refernce to', '#bar'],
    bar: 'bar',
    ...
}
```
You can insert other strings (of the same language) in your string by adding a string consisting of ```'#strId'``` where ```strId``` is the id of the string you wish to insert.

Currently this only works inside 'nested strings' and not on top-level like
```javascript
{
    ...
    bar: '#baz',
    baz: 'baz',
    ...
}
```

If you need it on top-level, you can make it an Array consisting of one element, open an Issue or implement it yourself.

Be cautious with references and do not build endless loops which will lead to a stack overflow e.g. like this:
```javascript
{
    ...
    foo: ['#foo', 'blah '],
    ...
}
```

# Future improvements of 'nested string' syntax:
At the moment, passing variables in the options is not supported because i did not need it. I could imagine it being done in a kind of template syntax like ```'${varName}'``` but without using real template strings. If you need that, feel free to implement it Yourself.