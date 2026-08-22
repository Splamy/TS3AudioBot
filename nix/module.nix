{ self }:
{
  config,
  lib,
  pkgs,
  ...
}:
let
  cfg = config.services.ts3audiobot;
  format = pkgs.formats.toml { };
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
  settings = lib.recursiveUpdate defaultSettings cfg.settings;
  generatedConfig = format.generate "ts3audiobot.toml" settings;
  configPath = "${cfg.dataDir}/ts3audiobot.toml";
in
{
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
      default = { };
      example = {
        configs.send_stats = false;
        web = {
          hosts = [ "localhost" ];
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

    openFirewall = lib.mkOption {
      type = lib.types.bool;
      default = false;
      description = "Open the configured web interface TCP port.";
    };
  };

  config = lib.mkIf cfg.enable {
    users.groups = lib.mkIf (cfg.group == "ts3audiobot") {
      ts3audiobot = { };
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
      wantedBy = [ "multi-user.target" ];
      after = [ "network-online.target" ];
      wants = [ "network-online.target" ];

      preStart = ''
        install -m 0600 ${generatedConfig} ${configPath}
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
        ReadWritePaths = [ cfg.dataDir ];
      };
    };

    networking.firewall.allowedTCPPorts = lib.mkIf cfg.openFirewall [
      settings.web.port
    ];
  };
}
