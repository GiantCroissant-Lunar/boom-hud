import React from 'react';
export interface DebugOverlayViewModel {
  motionTargets?: Record<string, Partial<Record<'opacity' | 'positionX' | 'positionY' | 'positionZ' | 'scaleX' | 'scaleY' | 'scaleZ' | 'rotation' | 'rotationX' | 'rotationY' | 'width' | 'height' | 'visibility' | 'text' | 'spriteFrame' | 'color', number | boolean | string | readonly number[]>>>;
  currentChunk?: unknown;
  fps?: unknown;
  memoryUsage?: unknown;
  playerPosition?: unknown;
  version?: unknown;
}

type BoomHudMotionProperty = 'opacity' | 'positionX' | 'positionY' | 'positionZ' | 'scaleX' | 'scaleY' | 'scaleZ' | 'rotation' | 'rotationX' | 'rotationY' | 'width' | 'height' | 'visibility' | 'text' | 'spriteFrame' | 'color';
type BoomHudMotionScalar = number | boolean | string | readonly number[];
type BoomHudMotionTargetState = Partial<Record<BoomHudMotionProperty, BoomHudMotionScalar>>;
type BoomHudMotionTargets = Record<string, BoomHudMotionTargetState>;
const asBool = (value: unknown, fallback = true) => typeof value === 'boolean' ? value : fallback;
const asText = (value: unknown, fallback = '') => value == null ? fallback : String(value);
const formatValue = (value: unknown, format?: string, fallback = '') => !format ? asText(value, fallback) : format.replace(/\{0(?:\:[^}]*)?\}/g, asText(value, fallback));
const clampPercent = (value: unknown) => `${Math.max(0, Math.min(100, typeof value === 'number' ? value : 0))}%`;
const asMotionNumber = (value: BoomHudMotionScalar | undefined) => typeof value === 'number' ? value : undefined;
const asMotionText = (value: BoomHudMotionScalar | undefined) => typeof value === 'string' ? value : undefined;
const getMotionTarget = (targets: BoomHudMotionTargets | undefined, id?: string) => id ? targets?.[id] : undefined;
const getMotionText = (targets: BoomHudMotionTargets | undefined, id?: string) => asMotionText(getMotionTarget(targets, id)?.text);
const getMotionSpriteFrame = (targets: BoomHudMotionTargets | undefined, id?: string) => asMotionText(getMotionTarget(targets, id)?.spriteFrame);
const getMotionStyle = (targets: BoomHudMotionTargets | undefined, id?: string): React.CSSProperties => {
  const target = getMotionTarget(targets, id);
  if (!target) return {};
  const style: React.CSSProperties = {};
  const transform: string[] = [];
  const opacity = asMotionNumber(target.opacity);
  if (opacity !== undefined) style.opacity = opacity;
  const width = asMotionNumber(target.width);
  if (width !== undefined) style.width = width;
  const height = asMotionNumber(target.height);
  if (height !== undefined) style.height = height;
  const color = asMotionText(target.color);
  if (color !== undefined) style.color = color;
  if (typeof target.visibility === 'boolean') style.visibility = target.visibility ? 'visible' : 'hidden';
  if (typeof target.visibility === 'string') style.visibility = target.visibility === 'hidden' || target.visibility === 'collapse' ? target.visibility : 'visible';
  const positionX = asMotionNumber(target.positionX) ?? 0;
  const positionY = asMotionNumber(target.positionY) ?? 0;
  const positionZ = asMotionNumber(target.positionZ) ?? 0;
  if (positionX !== 0 || positionY !== 0 || positionZ !== 0) transform.push(`translate3d(${positionX}px, ${positionY}px, ${positionZ}px)`);
  const scaleX = asMotionNumber(target.scaleX);
  if (scaleX !== undefined) transform.push(`scaleX(${scaleX})`);
  const scaleY = asMotionNumber(target.scaleY);
  if (scaleY !== undefined) transform.push(`scaleY(${scaleY})`);
  const scaleZ = asMotionNumber(target.scaleZ);
  if (scaleZ !== undefined) transform.push(`scale3d(1, 1, ${scaleZ})`);
  const rotation = asMotionNumber(target.rotation);
  if (rotation !== undefined) transform.push(`rotate(${rotation}deg)`);
  const rotationX = asMotionNumber(target.rotationX);
  if (rotationX !== undefined) transform.push(`rotateX(${rotationX}deg)`);
  const rotationY = asMotionNumber(target.rotationY);
  if (rotationY !== undefined) transform.push(`rotateY(${rotationY}deg)`);
  if (transform.length > 0) style.transform = transform.join(' ');
  return style;
};

export function DebugOverlayView(props: DebugOverlayViewModel): React.JSX.Element {
  return (
    <div className='boomhud-node boomhud-root' style={ { gap: '4', padding: '8 12', margin: '0', width: 'auto', height: 'auto', alignItems: 'flex-start', justifyContent: 'flex-start', fontWeight: '400', borderRadius: '4px', position: 'absolute', ...getMotionStyle(props.motionTargets, 'root') } } data-boomhud-id='root'>
      <div className='boomhud-node boomhud-header' style={ { display: 'flex', flexDirection: 'row', gap: '8', padding: '0', margin: '0', alignItems: 'center', justifyContent: 'flex-start', fontWeight: '400', position: 'absolute', ...getMotionStyle(props.motionTargets, 'header') } } data-boomhud-id='header'>
        <span className='boomhud-node boomhud-title' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontSize: '10px', fontWeight: '700', letterSpacing: '2px', ...getMotionStyle(props.motionTargets, 'title') } } data-boomhud-id='title'>
          {getMotionText(props.motionTargets, 'title') ?? ('DEBUG')}
        </span>
        <span className='boomhud-node boomhud-version' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontSize: '10px', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'version') } } data-boomhud-id='version'>
          {getMotionText(props.motionTargets, 'version') ?? (asText(props.version, ''))}
        </span>
      </div>
      <section className='boomhud-node boomhud-separator' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignSelf: 'stretch', height: '1px', alignItems: 'flex-start', justifyContent: 'flex-start', fontWeight: '400', opacity: 0.3, position: 'absolute', ...getMotionStyle(props.motionTargets, 'separator') } } data-boomhud-id='separator' />
      <div className='boomhud-node boomhud-stats-container' style={ { display: 'flex', flexDirection: 'column', gap: '2', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontWeight: '400', position: 'absolute', ...getMotionStyle(props.motionTargets, 'stats-container') } } data-boomhud-id='stats-container'>
        <div className='boomhud-node boomhud-fps-row' style={ { display: 'flex', flexDirection: 'row', gap: '8', padding: '0', margin: '0', alignSelf: 'stretch', alignItems: 'flex-start', justifyContent: 'space-between', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'fps-row') } } data-boomhud-id='fps-row'>
          <span className='boomhud-node boomhud-fps-label' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontSize: '12px', fontFamily: 'monospace', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'fps-label') } } data-boomhud-id='fps-label'>
            {getMotionText(props.motionTargets, 'fps-label') ?? ('FPS')}
          </span>
          <span className='boomhud-node boomhud-fps-value' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontSize: '12px', fontFamily: 'monospace', fontWeight: '700', ...getMotionStyle(props.motionTargets, 'fps-value') } } data-boomhud-id='fps-value'>
            {getMotionText(props.motionTargets, 'fps-value') ?? (formatValue(props.fps, '{0:F0}', ''))}
          </span>
        </div>
        <div className='boomhud-node boomhud-memory-row' style={ { display: 'flex', flexDirection: 'row', gap: '8', padding: '0', margin: '0', alignSelf: 'stretch', alignItems: 'flex-start', justifyContent: 'space-between', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'memory-row') } } data-boomhud-id='memory-row'>
          <span className='boomhud-node boomhud-memory-label' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontSize: '12px', fontFamily: 'monospace', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'memory-label') } } data-boomhud-id='memory-label'>
            {getMotionText(props.motionTargets, 'memory-label') ?? ('MEM')}
          </span>
          <span className='boomhud-node boomhud-memory-value' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontSize: '12px', fontFamily: 'monospace', fontWeight: '700', ...getMotionStyle(props.motionTargets, 'memory-value') } } data-boomhud-id='memory-value'>
            {getMotionText(props.motionTargets, 'memory-value') ?? (asText(props.memoryUsage, ''))}
          </span>
        </div>
        <div className='boomhud-node boomhud-position-row' style={ { display: 'flex', flexDirection: 'row', gap: '8', padding: '0', margin: '0', alignSelf: 'stretch', alignItems: 'flex-start', justifyContent: 'space-between', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'position-row') } } data-boomhud-id='position-row'>
          <span className='boomhud-node boomhud-position-label' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontSize: '12px', fontFamily: 'monospace', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'position-label') } } data-boomhud-id='position-label'>
            {getMotionText(props.motionTargets, 'position-label') ?? ('POS')}
          </span>
          <span className='boomhud-node boomhud-position-value' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontSize: '12px', fontFamily: 'monospace', fontWeight: '700', ...getMotionStyle(props.motionTargets, 'position-value') } } data-boomhud-id='position-value'>
            {getMotionText(props.motionTargets, 'position-value') ?? (asText(props.playerPosition, ''))}
          </span>
        </div>
        <div className='boomhud-node boomhud-chunk-row' style={ { display: 'flex', flexDirection: 'row', gap: '8', padding: '0', margin: '0', alignSelf: 'stretch', alignItems: 'flex-start', justifyContent: 'space-between', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'chunk-row') } } data-boomhud-id='chunk-row'>
          <span className='boomhud-node boomhud-chunk-label' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontSize: '12px', fontFamily: 'monospace', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'chunk-label') } } data-boomhud-id='chunk-label'>
            {getMotionText(props.motionTargets, 'chunk-label') ?? ('CHUNK')}
          </span>
          <span className='boomhud-node boomhud-chunk-value' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', fontSize: '12px', fontFamily: 'monospace', fontWeight: '700', ...getMotionStyle(props.motionTargets, 'chunk-value') } } data-boomhud-id='chunk-value'>
            {getMotionText(props.motionTargets, 'chunk-value') ?? (asText(props.currentChunk, ''))}
          </span>
        </div>
      </div>
    </div>
  );
}
export default DebugOverlayView;
