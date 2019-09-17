<template>
	<div>
		<section class="hero is-primary" style="margin-top:3em; margin-bottom:5em;">
			<div class="hero-body">
				<div class="container">
					<h1 class="title">TS3Audiobot</h1>
					<h2 class="subtitle">The selfhosted music bot of your choice!</h2>
				</div>
			</div>
		</section>

		<div class="">
			<div class="tile is-ancestor">
				<div class="tile">
					<router-link to="overview" tag="a" class="tile is-parent">
						<div class="tile is-child notification is-success has-text-centered">
							<b-icon icon="view-dashboard" size="is-large"></b-icon>
							<div class="content">Jump to the Infoboard</div>
						</div>
					</router-link>

					<router-link to="bots" tag="a" class="tile is-parent">
						<div class="tile is-child notification is-success has-text-centered">
							<b-icon icon="robot" size="is-large"></b-icon>
							<div class="content">Jump to your Bots overview</div>
						</div>
					</router-link>

					<router-link to tag="a" class="tile is-parent">
						<div class="tile is-child notification is-success has-text-centered">
							<b-icon icon="emoticon-cool-outline" size="is-large"></b-icon>
							<div class="content">Do something cool...</div>
						</div>
					</router-link>
				</div>
			</div>
		</div>
	</div>
	<!-- <div class="columns">
		<div class="column">
			<router-link to="home">
				<div class="card">
					<div class="card-content has-text-centered">
						<b-icon icon="view-dashboard" size="is-large"></b-icon>
						<div class="content">Jump to the Infoboard</div>
					</div>
				</div>
			</router-link>
		</div>
		<div class="column">
			<router-link to="home">
				<div class="card">
					<div class="card-content has-text-centered">
						<b-icon icon="view-dashboard" size="is-large"></b-icon>
						<div class="content">Jump to your Bots overview</div>
					</div>
				</div>
			</router-link>
		</div>
		<div class="column">Second column</div>
		<div class="column">Third column</div>
		<div class="column">Fourth column</div>
	</div>-->
</template>
