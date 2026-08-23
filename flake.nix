{
  description = "TS3AudioBot";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    tsdeclarations = {
      url = "github:ReSpeak/tsdeclarations/53a1580af9e09b7c80c2c83d16dba9d2a0fb09a8";
      flake = false;
    };
  };

  outputs = {
    self,
    nixpkgs,
    tsdeclarations,
  }: let
    supportedSystems = [
      "aarch64-linux"
      "x86_64-linux"
    ];
    forAllSystems = nixpkgs.lib.genAttrs supportedSystems;
    pkgsFor = system: import nixpkgs {inherit system;};
  in {
    packages = forAllSystems (
      system: let
        pkgs = pkgsFor system;
        version = pkgs.lib.removeSuffix "\n" (builtins.readFile ./version.txt);
        revision =
          if self ? shortRev
          then self.shortRev
          else if self ? dirtyShortRev
          then self.dirtyShortRev
          else null;

        webInterface = pkgs.stdenvNoCC.mkDerivation {
          pname = "ts3audiobot-webinterface";
          version = "unstable";
          src = ./WebInterface;

          yarnOfflineCache = pkgs.fetchYarnDeps {
            yarnLock = ./WebInterface/yarn.lock;
            hash = "sha256-0uAxnp9gSTBE93Bme/cdT4m8yUcD9tcCAXcSucIrlHI=";
          };

          nativeBuildInputs = with pkgs; [
            nodejs_22
            yarn
            yarnConfigHook
          ];

          buildPhase = ''
            runHook preBuild
            yarn --offline run build
            runHook postBuild
          '';

          installPhase = ''
            runHook preInstall
            mkdir -p "$out"
            cp -r dist/. "$out/"
            runHook postInstall
          '';
        };

        ts3audiobot = pkgs.buildDotnetModule {
          pname = "ts3audiobot";
          inherit version;
          src = self;

          postPatch = ''
            mkdir -p TSLib/Declarations
            cp -r ${tsdeclarations}/. TSLib/Declarations/
          '';

          projectFile = "TS3AudioBot/TS3AudioBot.csproj";
          nugetDeps = ./nix/deps.json;
          dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0_3xx;
          dotnet-runtime = pkgs.dotnet-runtime_10;
          executables = ["TS3AudioBot"];
          dotnetBuildFlags =
            [
              "-p:ContinuousIntegrationBuild=true"
            ]
            ++ pkgs.lib.optional (revision != null) "-p:SourceRevisionId=${revision}";

          runtimeDeps = [pkgs.libopus];
          makeWrapperArgs = [
            "--prefix PATH : ${
              pkgs.lib.makeBinPath [
                pkgs.ffmpeg
                pkgs.yt-dlp
              ]
            }"
          ];

          postInstall = ''
            mkdir -p "$out/share/ts3audiobot"
            cp -r ${webInterface} "$out/share/ts3audiobot/WebInterface"
          '';

          meta = {
            description = "Advanced music bot for TeamSpeak 3";
            homepage = "https://github.com/Splamy/TS3AudioBot";
            license = pkgs.lib.licenses.osl3;
            mainProgram = "TS3AudioBot";
            platforms = pkgs.lib.platforms.linux;
          };
        };

        updateDeps = pkgs.writeShellApplication {
          name = "update-ts3audiobot-deps";
          text = ''
            output="''${1:-nix/deps.json}"
            exec ${ts3audiobot.fetch-deps} "$output"
          '';
        };
      in {
        default = ts3audiobot;
        update-deps = updateDeps;
        web-interface = webInterface;
      }
    );

    apps = forAllSystems (
      system: let
        package = self.packages.${system}.default;
      in {
        default = {
          type = "app";
          program = "${package}/bin/TS3AudioBot";
          meta.description = "Run TS3AudioBot";
        };
      }
    );

    devShells = forAllSystems (
      system: let
        pkgs = pkgsFor system;
      in {
        default = pkgs.mkShell {
          packages = with pkgs; [
            dotnetCorePackages.sdk_10_0_3xx
            ffmpeg
            libopus
            nodejs_22
            yarn
            yt-dlp
          ];
        };
      }
    );

    nixosModules.default = import ./nix/module.nix {inherit self;};
  };
}
