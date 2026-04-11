import React, { useEffect, useRef, useState } from "react";
import { cancelRender, continueRender, delayRender } from "remotion";
import pressStart2pLatinWoff2 from "@fontsource/press-start-2p/files/press-start-2p-latin-400-normal.woff2";

type FontReadyGateProps = {
  children: React.ReactNode;
};

export const FontReadyGate: React.FC<FontReadyGateProps> = ({ children }) => {
  const [handle] = useState(() => delayRender("Wait for web fonts"));
  const resumedRef = useRef(false);
  const [ready, setReady] = useState(() => {
    if (typeof document === "undefined") {
      return true;
    }

    return !("fonts" in document);
  });

  useEffect(() => {
    if (ready) {
      if (!resumedRef.current) {
        resumedRef.current = true;
        continueRender(handle);
      }

      return;
    }

    let cancelled = false;
    ensurePressStartFontReady()
      .then(() => {
        if (cancelled) {
          return;
        }

        setReady(true);
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          cancelRender(error);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [handle, ready]);

  if (!ready) {
    return null;
  }

  return <>{children}</>;
};

const ensurePressStartFontReady = async (): Promise<void> => {
  const familyName = "Press Start 2P";
  if (!document.fonts.check(`12px "${familyName}"`)) {
    const font = new FontFace(
      familyName,
      `url(${pressStart2pLatinWoff2}) format('woff2')`,
      { style: "normal", weight: "400" },
    );
    await font.load();
    document.fonts.add(font);
  }

  await document.fonts.ready;
};
