import strings from './strings';

/**
 * Gets a localized string for specific key and language if available, otherwise in default language.
 * @param {string} key - String key
 * @param {string} language - desired language
 * @param {Object} options - additional options
 * @return {string} requested string in requested language, if available.
 */
export function getString (key, language, options) {
  if (language && strings[language] && strings[language][key]) {
    let string = strings[language][key];
    if (!(typeof string === 'string')) {
      return parseNestedString(string, language, options);
    } else {
      return string;
    }
  } else if (strings.en && strings.en[key]) {
    let string = strings.en[key];
    if (!(typeof string === 'string')) {
      return parseNestedString(string, language, options);
    } else {
      return string;
    }
  } else {
    return key;
  }
}

/**
 * Detects the language that the user has set for their browser.
 * @return {string} - one of the supported languages.
 */
export function detectLanguage () {
  let lang = navigator.language;
  lang = lang.split('-')[0].toLowerCase();
  if (Object.keys(strings).includes(lang)) {
    return lang;
  } else {
    return 'en';
  }
}

export const supportedLanguages = Object.freeze(Object.keys(strings));

function parseNestedString (object, language, options) {
  if (typeof object === 'string' || object instanceof String) {
    if (object.length > 1 && object.startsWith('#')) {
      return getString(object.substring(1), language, options);
    } else {
      return object;
    }
  } else if (object instanceof Array) {
    let newString = '';
    object.forEach(function (entry) {
      newString += parseNestedString(entry, language, options);
    });
    return newString;
  } else if (typeof object === 'object' && Object.keys(object).length > 0) {
    const optionName = Object.keys(object)[0];
    let decision = object[optionName];
    if (options && Object.keys(options).includes(optionName) && Object.keys(decision).includes(String(options[optionName]))) {
      const optionValue = String(options[optionName]);
      return parseNestedString(decision[optionValue], language);
    } else if (Object.keys(decision).includes('default')) {
      return parseNestedString(decision.default, language);
    } else if (Object.keys(decision).length > 0) {
      return parseNestedString(decision[Object.keys(decision)[0]], language);
    } else {
      return '';
    }
  } else {
    return '';
  }
}
