import React, { useEffect, useRef, useState } from "react";
import { cancelRender, continueRender, delayRender, staticFile } from "remotion";

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
  const familyName = "BoomHudPressStart2P";
  const sourcePath = staticFile("fonts/press-start-2p-latin-400-normal.woff2");
  const styleId = "boomhud-press-start-font";

  if (!document.getElementById(styleId)) {
    const style = document.createElement("style");
    style.id = styleId;
    style.textContent = `
      @font-face {
        font-family: '${familyName}';
        src: url('${sourcePath}') format('woff2');
        font-style: normal;
        font-weight: 400;
        font-display: block;
      }
    `;
    document.head.appendChild(style);
  }

  await document.fonts.load(`400 12px "${familyName}"`, "QuestSidebar");
  await document.fonts.ready;
};
