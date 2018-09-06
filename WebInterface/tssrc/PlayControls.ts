class PlayControls {
    public static enable() {
        const divPlayCtrl = Util.getElementByIdSafe("playblock");
        divPlayCtrl.classList.remove("playdisabled");
    }

    public static disable() {
        const divPlayCtrl = Util.getElementByIdSafe("playblock");
        divPlayCtrl.classList.add("playdisabled");
    }
}
