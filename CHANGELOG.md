# Changelog

## 1.0.2

- Remove the grey left edge from the green ISA reference crop.
- On/Off and mph/km/h controls display their current state.
- Verify that changing display units preserves the physical road limit, including mph buses
  on maps whose road rules use km/h.
- Publish as a standalone MIT-licensed resource with SDK build instructions and policy tests.

## 1.0.1

- Retain road limits through five seconds of missing path data.
- Use a minimal bottom-right HUD with transparent reference icons and controls shown on Ctrl.
- Render numbers separately inside the speed-limit ring and flash the sign on overspeed.
