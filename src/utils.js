/*
 *the idea of the following function was taken from https://stackoverflow.com/a/14561433
 *by User talkol (https://stackoverflow.com/users/1025458/talkol).
 *The License is CC BY-SA 4.0 (https://creativecommons.org/licenses/by-sa/4.0/)
 *The Code was slightly adapted.
 */
/**
 * @param {number} lat1
 * @param {number} lon1
 * @param {number} lat2
 * @param {number} lon2
 * @return {number}
 */
export function checkDistance (lat1, lon1, lat2, lon2) {
  const R = 6371;
  let x1 = lat2 - lat1;
  let dLat = (x1 * Math.PI) / 180;
  let x2 = lon2 - lon1;
  let dLon = (x2 * Math.PI) / 180;
  let a = Math.sin(dLat / 2) * Math.sin(dLat / 2) + Math.cos((lat1 * Math.PI) / 180) * Math.cos((lat2 * Math.PI) / 180) * Math.sin(dLon / 2) * Math.sin(dLon / 2);
  let c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  let d = R * c;
  return d;
}

/**
 * @param {string} id - the id of the input field to copy from
 * @return {bool} - returns if copying was successful
 */
export function copyfieldvalue (id) {
  let field = document.getElementById(id);
  field.focus();
  field.setSelectionRange(0, field.value.length);
  field.select();
  return copySelectionText();
}

function copySelectionText () {
  let copysuccess;
  try {
    copysuccess = document.execCommand('copy');
  } catch (e) {
    copysuccess = false;
  }
  return copysuccess;
}

const request = new XMLHttpRequest();

// source: Oscar Zanota on Dev.to (https://dev.to/oskarcodes/send-automated-discord-messages-through-webhooks-using-javascript-1p01)
export function sendMessage (msg) {
  let params = {
    username: window.plugin.pnav.settings.name,
    avatar_url: '',
    content: msg
  };
  request.open('POST', window.plugin.pnav.settings.webhookUrl);
  request.setRequestHeader('Content-type', 'application/json');
  request.send(JSON.stringify(params), false);
}
