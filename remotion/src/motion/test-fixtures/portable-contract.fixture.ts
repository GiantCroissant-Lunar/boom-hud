export const portableContractMotionPayload = {
  $schema: "https://schemas.boomhud.dev/motion.schema.json",
  version: "1.0",
  name: "PartyHudMotion",
  fps: 30,
  clips: [
    {
      id: "intro",
      name: "Intro",
      durationFrames: 20,
      tracks: [
        {
          id: "rootTrack",
          targetId: "root",
          targetKind: "root",
          channels: [
            {
              property: "opacity",
              keyframes: [
                { frame: 0, value: { kind: "number", number: 0.25 } },
                { frame: 10, value: { kind: "number", number: 1 } },
              ],
            },
          ],
        },
        {
          id: "portraitTrack",
          targetId: "char1",
          targetKind: "component",
          channels: [
            {
              property: "positionX",
              keyframes: [
                { frame: 0, value: { kind: "number", number: 0 } },
                {
                  frame: 12,
                  value: { kind: "number", number: 18 },
                  easing: "easeOut",
                },
              ],
            },
          ],
        },
        {
          id: "nameTrack",
          targetId: "char1/name",
          targetKind: "element",
          channels: [
            {
              property: "text",
              keyframes: [
                {
                  frame: 0,
                  value: { kind: "text", text: "Ready" },
                  easing: "step",
                },
                {
                  frame: 8,
                  value: { kind: "text", text: "Aelric" },
                  easing: "step",
                },
              ],
            },
          ],
        },
        {
          id: "attackButtonTrack",
          targetId: "char1/attackButton",
          targetKind: "component",
          channels: [
            {
              property: "opacity",
              keyframes: [
                { frame: 0, value: { kind: "number", number: 0.5 } },
                {
                  frame: 12,
                  value: { kind: "number", number: 1 },
                  easing: "easeOut",
                },
              ],
            },
          ],
        },
      ],
    },
  ],
} as const;
