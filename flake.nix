{
  description = "TS3AudioBot";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    self.submodules = true;
  };

  outputs =
    {
      self,
      nixpkgs,
    }:
    let
      supportedSystems = [
        "aarch64-linux"
        "x86_64-linux"
      ];
      forAllSystems = nixpkgs.lib.genAttrs supportedSystems;
      pkgsFor = system: import nixpkgs { inherit system; };
    in
    {
      packages = forAllSystems (
        system:
        let
          pkgs = pkgsFor system;
          version = "0.0.0-unstable";
          revision = self.shortRev or "unknown";

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

            projectFile = "TS3AudioBot/TS3AudioBot.csproj";
            nugetDeps = ./nix/deps.json;
            dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0_3xx;
            dotnet-runtime = pkgs.dotnet-runtime_10;
            executables = [ "TS3AudioBot" ];

            postPatch = ''
              substituteInPlace TS3AudioBot/TS3AudioBot.csproj \
                --replace-fail \
                '<Target Name="GenerateGitVersion" BeforeTargets="BeforeCompile">' \
                '<Target Name="GenerateGitVersion" BeforeTargets="BeforeCompile" Condition="false">'

              cat > TS3AudioBot/Version.g.cs <<'EOF'
              namespace TS3AudioBot.Environment;

              partial class BuildData
              {
                  partial void GetDataInternal()
                  {
                      Version = "${version}";
                      Branch = "nix";
                      CommitSha = "${revision}";
                      BuildConfiguration = "Release";
                  }
              }
              EOF
            '';

            runtimeDeps = [ pkgs.libopus ];
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
        in
        {
          default = ts3audiobot;
          update-deps = updateDeps;
          web-interface = webInterface;
        }
      );

      apps = forAllSystems (
        system:
        let
          package = self.packages.${system}.default;
        in
        {
          default = {
            type = "app";
            program = "${package}/bin/TS3AudioBot";
            meta.description = "Run TS3AudioBot";
          };

          update-deps = {
            type = "app";
            program = "${self.packages.${system}.update-deps}/bin/update-ts3audiobot-deps";
            meta.description = "Update the TS3AudioBot NuGet dependency lock";
          };
        }
      );

      devShells = forAllSystems (
        system:
        let
          pkgs = pkgsFor system;
        in
        {
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

      nixosModules.default = import ./nix/module.nix { inherit self; };
    };
}
