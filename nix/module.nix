{self}: {
  config,
  lib,
  pkgs,
  ...
}: let
  cfg = config.services.ts3audiobot;
  format = pkgs.formats.toml {};
  defaultSettings = {
    configs.bots_path = "bots";
    db.path = "ts3audiobot.db";
    plugins.path = "plugins";
    rights.path = "rights.toml";
    tools = {
      ffmpeg.path = "${pkgs.ffmpeg}/bin/ffmpeg";
      "youtube-dl".path = "${pkgs.yt-dlp}/bin/yt-dlp";
    };
    web.interface.path = "${cfg.package}/share/ts3audiobot/WebInterface";
  };
  managedPaths =
    lib.optionalAttrs (cfg.bots != {}) {
      configs.bots_path = "bots";
    }
    // lib.optionalAttrs (cfg.rights != null) {
      rights.path = "rights.toml";
    };
  settings = lib.recursiveUpdate (lib.recursiveUpdate defaultSettings cfg.settings) managedPaths;
  generatedConfig = format.generate "ts3audiobot.toml" settings;
  generatedBots =
    lib.mapAttrs (
      name: botSettings: format.generate "ts3audiobot-${name}-bot.toml" botSettings
    )
    cfg.bots;
  generatedRights =
    if cfg.rights == null
    then null
    else format.generate "ts3audiobot-rights.toml" cfg.rights;
  configPath = "${cfg.dataDir}/ts3audiobot.toml";
  botsPath = "${cfg.dataDir}/bots";
  rightsPath = "${cfg.dataDir}/rights.toml";
in {
  options.services.ts3audiobot = {
    enable = lib.mkEnableOption "TS3AudioBot";

    package = lib.mkOption {
      type = lib.types.package;
      default = self.packages.${pkgs.stdenv.hostPlatform.system}.default;
      defaultText = lib.literalExpression "self.packages.\${pkgs.stdenv.hostPlatform.system}.default";
      description = "TS3AudioBot package to use.";
    };

    user = lib.mkOption {
      type = lib.types.str;
      default = "ts3audiobot";
      description = "User account under which TS3AudioBot runs.";
    };

    group = lib.mkOption {
      type = lib.types.str;
      default = "ts3audiobot";
      description = "Group under which TS3AudioBot runs.";
    };

    dataDir = lib.mkOption {
      type = lib.types.path;
      default = "/var/lib/ts3audiobot";
      description = "Writable directory for configuration, bot data, plugins, and logs.";
    };

    settings = lib.mkOption {
      type = format.type;
      default = {};
      example = {
        configs.send_stats = false;
        web = {
          hosts = ["localhost"];
          port = 58913;
        };
      };
      description = ''
        TS3AudioBot configuration written to ts3audiobot.toml. The module supplies
        suitable defaults for state paths, ffmpeg, yt-dlp, and the web interface.
        Values containing secrets are copied through the world-readable Nix store;
        configure those in the writable bot configuration files instead.
      '';
    };

    bots = lib.mkOption {
      type = lib.types.attrsOf format.type;
      default = {};
      example = {
        myBot = {
          run = true;
          connect = {
            address = "teamspeak.example.org";
            name = "MusicBot";
          };
          audio.bitrate = 96;
        };
      };
      description = ''
        Named bot configurations written to ./bots/<name>/bot.toml relative to dataDir.
        These files are replaced whenever the service starts. Bot names must be safe
        file names. Values containing secrets are copied through the world-readable
        Nix store.
      '';
    };

    rights = lib.mkOption {
      type = lib.types.nullOr format.type;
      default = null;
      example = {
        "+" = [
          "cmd.help.*"
          "cmd.version"
        ];
        rule = [
          {
            groupid = [6];
            "+" = "*";
          }
        ];
      };
      description = ''
        Rights configuration written to ./rights.toml relative to dataDir. When null,
        TS3AudioBot creates and manages its default rights file. When configured, the
        file is replaced whenever the service starts. Values containing secrets are
        copied through the world-readable Nix store.
      '';
    };

    openFirewall = lib.mkOption {
      type = lib.types.bool;
      default = false;
      description = "Open the configured web interface TCP port.";
    };
  };

  config = lib.mkIf cfg.enable {
    assertions = [
      {
        assertion = lib.all (
          name: name != "" && name != "." && name != ".." && builtins.match "^[A-Za-z0-9._-]+$" name != null
        ) (lib.attrNames cfg.bots);
        message = "services.ts3audiobot.bots names may only contain letters, numbers, '.', '_', and '-'.";
      }
    ];

    users.groups = lib.mkIf (cfg.group == "ts3audiobot") {
      ts3audiobot = {};
    };
    users.users = lib.mkIf (cfg.user == "ts3audiobot") {
      ts3audiobot = {
        description = "TS3AudioBot service user";
        group = cfg.group;
        home = cfg.dataDir;
        isSystemUser = true;
      };
    };

    systemd.tmpfiles.rules = [
      "d ${cfg.dataDir} 0750 ${cfg.user} ${cfg.group} - -"
    ];

    systemd.services.ts3audiobot = {
      description = "TS3AudioBot";
      wantedBy = ["multi-user.target"];
      after = ["network-online.target"];
      wants = ["network-online.target"];

      preStart = ''
        install -m 0600 ${generatedConfig} ${lib.escapeShellArg configPath}
        mkdir -p ${lib.escapeShellArg botsPath}
        ${lib.concatMapAttrsStringSep "\n" (name: generatedBot: ''
            mkdir -p ${lib.escapeShellArg "${botsPath}/${name}"}
            install -m 0600 ${generatedBot} ${lib.escapeShellArg "${botsPath}/${name}/bot.toml"}
          '')
          generatedBots}
        ${lib.optionalString (generatedRights != null) ''
          install -m 0600 ${generatedRights} ${lib.escapeShellArg rightsPath}
        ''}
      '';

      serviceConfig = {
        Type = "simple";
        User = cfg.user;
        Group = cfg.group;
        WorkingDirectory = cfg.dataDir;
        ExecStart = "${lib.getExe cfg.package} --config ${configPath} --non-interactive";
        Restart = "on-failure";
        KillSignal = "SIGINT";

        NoNewPrivileges = true;
        PrivateTmp = true;
        ProtectHome = true;
        ProtectSystem = "strict";
        ReadWritePaths = [cfg.dataDir];
      };
    };

    networking.firewall.allowedTCPPorts = lib.mkIf cfg.openFirewall [
      settings.web.port
    ];
  };
}
