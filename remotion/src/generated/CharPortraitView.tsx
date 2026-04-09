import React from 'react';
export interface CharPortraitViewModel {
  motionTargets?: Record<string, Partial<Record<'opacity' | 'positionX' | 'positionY' | 'positionZ' | 'scaleX' | 'scaleY' | 'scaleZ' | 'rotation' | 'rotationX' | 'rotationY' | 'width' | 'height' | 'visibility' | 'text' | 'spriteFrame' | 'color', number | boolean | string | readonly number[]>>>;
}

type BoomHudMotionProperty = 'opacity' | 'positionX' | 'positionY' | 'positionZ' | 'scaleX' | 'scaleY' | 'scaleZ' | 'rotation' | 'rotationX' | 'rotationY' | 'width' | 'height' | 'visibility' | 'text' | 'spriteFrame' | 'color';
type BoomHudMotionScalar = number | boolean | string | readonly number[];
type BoomHudMotionTargetState = Partial<Record<BoomHudMotionProperty, BoomHudMotionScalar>>;
type BoomHudMotionTargets = Record<string, BoomHudMotionTargetState>;
const asBool = (value: unknown, fallback = true) => typeof value === 'boolean' ? value : fallback;
const asText = (value: unknown, fallback = '') => value == null ? fallback : String(value);
const resolveIconText = (value: unknown, familyName?: string) => {
  const text = asText(value, '');
  if (!text || familyName?.trim().toLowerCase() !== 'lucide') return text;
  switch (text) {
    case 'sword': return '†';
    case 'swords': return '⚔';
    case 'sparkles': return '✦';
    case 'wand-sparkles': return '✦';
    case 'shield': return '⛨';
    case 'flask-conical': return '⚗';
    case 'flame': return '✹';
    case 'moon': return '☾';
    case 'cross': return '✚';
    default: return text;
  }
};
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

export function CharPortraitView(props: CharPortraitViewModel): React.JSX.Element {
  return (
    <div className='boomhud-node boomhud-component-charportrait' style={ { display: 'flex', flexDirection: 'column', gap: '8px', padding: '0', margin: '0', width: '130px', alignItems: 'center', justifyContent: 'flex-start', color: '#000000', backgroundColor: '#000000', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'Component/CharPortrait') } } data-boomhud-id='Component/CharPortrait'>
      <div className='boomhud-node boomhud-face' style={ { padding: '0', margin: '0', width: '56px', height: '56px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#000000', backgroundColor: '#000000', fontWeight: '400', borderStyle: 'solid', borderWidth: '6px', borderColor: '#FFFFFF', position: 'relative', ...getMotionStyle(props.motionTargets, 'face') } } data-boomhud-id='face'>
        <span className='boomhud-node boomhud-classicon' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', width: '32px', height: '32px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontFamily: 'lucide', fontWeight: '400', position: 'absolute', left: '12px', top: '12px', ...getMotionStyle(props.motionTargets, 'classIcon') } } data-boomhud-id='classIcon'>
          {resolveIconText(getMotionText(props.motionTargets, 'classIcon') ?? ('shield'), 'lucide')}
        </span>
      </div>
      <span className='boomhud-node boomhud-name' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontSize: '8px', fontFamily: 'Press Start 2P', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'name') } } data-boomhud-id='name'>
        {getMotionText(props.motionTargets, 'name') ?? ('Name')}
      </span>
      <div className='boomhud-node boomhud-hp' style={ { padding: '0', margin: '0', alignSelf: 'stretch', height: '10px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#333333', backgroundColor: '#333333', fontWeight: '400', position: 'relative', ...getMotionStyle(props.motionTargets, 'hp') } } data-boomhud-id='hp'>
        <section className='boomhud-node boomhud-hpfill' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', width: '90px', height: '10px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#CCCCCC', backgroundColor: '#CCCCCC', fontWeight: '400', position: 'absolute', left: '0px', top: '0px', ...getMotionStyle(props.motionTargets, 'hpFill') } } data-boomhud-id='hpFill' />
      </div>
      <div className='boomhud-node boomhud-mp' style={ { padding: '0', margin: '0', alignSelf: 'stretch', height: '8px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#333333', backgroundColor: '#333333', fontWeight: '400', position: 'relative', ...getMotionStyle(props.motionTargets, 'mp') } } data-boomhud-id='mp'>
        <section className='boomhud-node boomhud-mpfill' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', width: '60px', height: '8px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#777777', backgroundColor: '#777777', fontWeight: '400', position: 'absolute', left: '0px', top: '0px', ...getMotionStyle(props.motionTargets, 'mpFill') } } data-boomhud-id='mpFill' />
      </div>
      <div className='boomhud-node boomhud-stats' style={ { display: 'flex', flexDirection: 'row', gap: '8px', padding: '0 6px', margin: '0', alignSelf: 'stretch', alignItems: 'flex-start', justifyContent: 'center', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'stats') } } data-boomhud-id='stats'>
        <span className='boomhud-node boomhud-stat1' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#AAAAAA', fontSize: '8px', fontFamily: 'Press Start 2P', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'stat1') } } data-boomhud-id='stat1'>
          {getMotionText(props.motionTargets, 'stat1') ?? ('ATK 10')}
        </span>
        <span className='boomhud-node boomhud-stat2' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#AAAAAA', fontSize: '8px', fontFamily: 'Press Start 2P', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'stat2') } } data-boomhud-id='stat2'>
          {getMotionText(props.motionTargets, 'stat2') ?? ('DEF 8')}
        </span>
      </div>
      <div className='boomhud-node boomhud-actiongrid' style={ { display: 'flex', flexDirection: 'row', gap: '2px', padding: '0', margin: '0', alignSelf: 'stretch', alignItems: 'flex-start', justifyContent: 'flex-start', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'actionGrid') } } data-boomhud-id='actionGrid'>
        <div className='boomhud-node boomhud-atk' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', flex: '1 1 0', height: '28px', alignItems: 'center', justifyContent: 'center', color: '#000000', backgroundColor: '#000000', fontWeight: '400', borderStyle: 'solid', borderWidth: '2px', borderColor: '#FFFFFF', ...getMotionStyle(props.motionTargets, 'atk') } } data-boomhud-id='atk'>
          <span className='boomhud-node boomhud-qepo3' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', width: '16px', height: '16px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontFamily: 'lucide', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'qEpO3') } } data-boomhud-id='qEpO3'>
            {resolveIconText(getMotionText(props.motionTargets, 'qEpO3') ?? ('swords'), 'lucide')}
          </span>
        </div>
        <div className='boomhud-node boomhud-mag' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', flex: '1 1 0', height: '28px', alignItems: 'center', justifyContent: 'center', color: '#000000', backgroundColor: '#000000', fontWeight: '400', borderStyle: 'solid', borderWidth: '2px', borderColor: '#FFFFFF', ...getMotionStyle(props.motionTargets, 'mag') } } data-boomhud-id='mag'>
          <span className='boomhud-node boomhud-aiphn' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', width: '16px', height: '16px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontFamily: 'lucide', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'AIphN') } } data-boomhud-id='AIphN'>
            {resolveIconText(getMotionText(props.motionTargets, 'AIphN') ?? ('wand-sparkles'), 'lucide')}
          </span>
        </div>
        <div className='boomhud-node boomhud-def' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', flex: '1 1 0', height: '28px', alignItems: 'center', justifyContent: 'center', color: '#000000', backgroundColor: '#000000', fontWeight: '400', borderStyle: 'solid', borderWidth: '2px', borderColor: '#FFFFFF', ...getMotionStyle(props.motionTargets, 'def') } } data-boomhud-id='def'>
          <span className='boomhud-node boomhud-e4qkz' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', width: '16px', height: '16px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontFamily: 'lucide', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'e4QKZ') } } data-boomhud-id='e4QKZ'>
            {resolveIconText(getMotionText(props.motionTargets, 'e4QKZ') ?? ('shield'), 'lucide')}
          </span>
        </div>
        <div className='boomhud-node boomhud-item' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', flex: '1 1 0', height: '28px', alignItems: 'center', justifyContent: 'center', color: '#000000', backgroundColor: '#000000', fontWeight: '400', borderStyle: 'solid', borderWidth: '2px', borderColor: '#FFFFFF', ...getMotionStyle(props.motionTargets, 'item') } } data-boomhud-id='item'>
          <span className='boomhud-node boomhud-dvzx7' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', width: '16px', height: '16px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontFamily: 'lucide', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'dVzX7') } } data-boomhud-id='dVzX7'>
            {resolveIconText(getMotionText(props.motionTargets, 'dVzX7') ?? ('flask-conical'), 'lucide')}
          </span>
        </div>
      </div>
    </div>
  );
}
export default CharPortraitView;
