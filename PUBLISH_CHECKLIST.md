# Binary release gates

[Русский](PUBLISH_CHECKLIST_RU.md) · [Source/QA/public versions](docs/CURRENT_STATUS.md)

A source checkpoint push is not a binary release. Do not inherit old release checkboxes or publish automatically.

- [ ] Identify the exact candidate commit, matching DLL pair, host compatibility and supported feature scope.
- [ ] Run affected build/localization/geometry/store/host checks; UI/fog checks when changed. Record inherited failures separately.
- [ ] Deploy through the guarded authorized QA path while Valheim is closed; verify hashes and protected data.
- [ ] Pass the applicable native visual/input/construction/cancel/resource/reload cases in supported languages/resolutions.
- [ ] Complete host+remote/reconnect/restart/permissions checks for features advertised as network-ready.
- [ ] Document unsupported formats/state, missing models, limits and outstanding work. Do not advertise a lab renderer as a persistent house.
- [ ] Verify package provenance/license, own assets only, no game DLLs/saves/tokens/logs or test authority bypasses.
- [ ] Obtain explicit owner approval for this binary release, then create versioned notes/manifests/hashes from the accepted pair.

Current .206 is installed QA, not a public package; unfinished house/board and full module acceptance remain open. Historical [.37 checklist](docs/PUBLISH_CHECKLIST_0.19.37.md) and published alpha assets retain their original scope. [LICENSE](LICENSE) is unchanged.
