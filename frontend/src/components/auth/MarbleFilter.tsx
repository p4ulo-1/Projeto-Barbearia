export function MarbleFilter() {
  return (
    <svg width="0" height="0" style={{ position: "absolute" }} aria-hidden="true" focusable="false">
      <filter id="marmore" x="0" y="0" width="100%" height="100%" colorInterpolationFilters="sRGB">
        <feTurbulence
          type="fractalNoise"
          baseFrequency="0.0065 0.0095"
          numOctaves={2}
          seed={11}
          result="ruido"
        >
          <animate
            attributeName="baseFrequency"
            dur="46s"
            values="0.0065 0.0095;0.0095 0.0065;0.0065 0.0095"
            repeatCount="indefinite"
          />
        </feTurbulence>
        <feDisplacementMap
          in="SourceGraphic"
          in2="ruido"
          scale={300}
          xChannelSelector="R"
          yChannelSelector="G"
        />
      </filter>
    </svg>
  );
}
