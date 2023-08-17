import {getString as gs} from '../l10n';

/**
 * Template helper for Handlebars to get localized strings.
 * @this {{language:string}} Template context
 * @param {string} arg Key of the string to localize
 * @return Localized string
 */
export default function getString (arg) {
  return gs(arg, this.language);
}
