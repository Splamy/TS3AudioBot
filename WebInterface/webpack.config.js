const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin')
const CopyPlugin = require('copy-webpack-plugin');
const VueLoaderPlugin = require('vue-loader/lib/plugin')

module.exports = {
	entry: './src/ts/Main.ts',
	module: {
		rules: [
			{
				test: /\.tsx?$/i,
				use: [{
					loader: 'ts-loader',
					options: {
						appendTsSuffixTo: [/\.vue$/]
					}
				}],
				exclude: /node_modules/
			},
			{
				test: /\.less$/i,
				use: [
					'style-loader',
					'css-loader',
					'less-loader',
				],
			},
			{
				test: /\.svg$/i,
				loader: 'svg-inline-loader'
			},
			{
				test: /\.vue$/i,
				loader: 'vue-loader'
			},
			{
				test: /\.css$/i,
				use: [
					'vue-style-loader',
					'css-loader',
				],
			},
			{
				test: /\.(jpe?g|png|gif|svg|eot|woff|ttf|svg|woff2)$/i,
				use: [
					{
						loader: 'file-loader',
						options: {
							name: "[path][name].[ext]"
						}
					}
				]
			}
		]
	},
	resolve: {
		extensions: ['.tsx', '.ts', '.js', '.vue'],
		alias: {
			'vue$': 'vue/dist/vue.esm.js'
		}
	},
	output: {
		filename: 'bundle.js',
		path: path.resolve(__dirname, 'dist')
	},
	plugins: [
		new HtmlWebpackPlugin({
			title: 'TS3AudioBot',
			template: 'src/html/index.html'
		}),
		new CopyPlugin({
			patterns: [
				{ from: 'src/html', to: '.' },
			]
		}),
		new VueLoaderPlugin()
	],
	devServer: {
		disableHostCheck: true,
		headers: {
			"Access-Control-Allow-Origin": "*",
			"Access-Control-Allow-Methods": "GET",
			"Access-Control-Allow-Headers": "X-Requested-With, Content-Type, Authorization"
		}
	}
};
