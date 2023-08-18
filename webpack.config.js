/* eslint-disable strict */
const path = require('path');
const {UserscriptPlugin} = require('webpack-userscript');
const WrapperPlugin = require('./wrapper-webpack-plugin');
const dev = process.env.NODE_ENV == 'development';

const SCRIPT_HEADER = `function wrapper(SCRIPT_INFO) {
if (typeof window.plugin !== 'function') window.plugin = function () { };
`;
const SCRIPT_FOOTER = `
};

(function () {
  const info = {};
  if (typeof GM_info !== 'undefined' && GM_info && GM_info.script)
    info.script = { version: GM_info.script.version, name: GM_info.script.name, description: GM_info.script.description };
  if (typeof unsafeWindow != 'undefined' || typeof GM_info == 'undefined' || GM_info.scriptHandler != 'Tampermonkey') {
    const script = document.createElement('script');
    script.appendChild(document.createTextNode( '('+ wrapper +')('+JSON.stringify(info)+');'));
    document.head.appendChild(script);} 
  else wrapper(info);
})();`;

module.exports = {
  mode: dev ? 'development' : 'production',
  entry: path.resolve(__dirname, 'src', 'PNavCopy.js'),
  output: {
    path: path.resolve(__dirname, 'dist'),
    filename: 'PNavCopy.user.js',
    publicPath: '/'
  },
  module: {
    rules: [
      {
        test: /\.hbs$/,
        loader: 'handlebars-loader'
      }
    ]
  },
  devServer: {
    static: {
      directory: path.join(__dirname, 'dist')
    },
    client: false,
    hot: false,
    liveReload: false,
    webSocketServer: false
  },
  devtool: 'inline-cheap-source-map',
  plugins: [
    new WrapperPlugin({
      header: SCRIPT_HEADER,
      footer: SCRIPT_FOOTER,
      afterOptimizations: true
    }),
    new UserscriptPlugin({
      headers: {
        name: 'IITC Plugin: Copy PokeNav Command',
        category: 'Misc',
        namespace: 'https://github.com/MaxEtMoritz/PNavCopy',
        include: [
          'http://intel.ingress.com/*',
          'https://intel.ingress.com/*'
        ],
        grant: 'none',
        id: 'pnavcopy@maxetmoritz'
      },
      metajs: !dev,
      downloadBaseURL: 'https://github.com/MaxEtMoritz/PNavCopy/releases/latest/download/',
      whitelist: [
        'category',
        'id'
      ],
      proxyScript: dev
    })
  ]
};
