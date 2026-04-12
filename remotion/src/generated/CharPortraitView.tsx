import React from 'react';
export interface CharPortraitViewModel {
  motionTargets?: Record<string, Partial<Record<'opacity' | 'positionX' | 'positionY' | 'positionZ' | 'scaleX' | 'scaleY' | 'scaleZ' | 'rotation' | 'rotationX' | 'rotationY' | 'width' | 'height' | 'visibility' | 'text' | 'spriteFrame' | 'color', number | boolean | string>>>;
  motionScope?: string;
}

type BoomHudMotionProperty = 'opacity' | 'positionX' | 'positionY' | 'positionZ' | 'scaleX' | 'scaleY' | 'scaleZ' | 'rotation' | 'rotationX' | 'rotationY' | 'width' | 'height' | 'visibility' | 'text' | 'spriteFrame' | 'color';
type BoomHudMotionScalar = number | boolean | string;
type BoomHudMotionTargetState = Partial<Record<BoomHudMotionProperty, BoomHudMotionScalar>>;
type BoomHudMotionTargets = Record<string, BoomHudMotionTargetState>;
const asBool = (value: unknown, fallback = true) => typeof value === 'boolean' ? value : fallback;
const asText = (value: unknown, fallback = '') => value == null ? fallback : String(value);
const resolveMotionId = (scope: string | undefined, id?: string) => !id ? undefined : scope ? `${scope}/${id}` : id;
const renderLucideIcon = (token: string): React.JSX.Element | string => {
  const common = {
    width: '100%',
    height: '100%',
    viewBox: '0 0 24 24',
    fill: 'none',
    stroke: 'currentColor',
    strokeWidth: 2,
    strokeLinecap: 'round' as const,
    strokeLinejoin: 'round' as const,
    'aria-hidden': true
  };
  switch (token) {
    case 'cross': return <svg {...common}><path d='M12 5v14' /><path d='M5 12h14' /></svg>;
    case 'shield': return <svg {...common}><path d='M12 3l7 3v6c0 5-3.5 8.8-7 9-3.5-.2-7-4-7-9V6l7-3Z' /></svg>;
    case 'flame': return <svg {...common}><path d='M12 3s4 4 4 8a4 4 0 1 1-8 0c0-2.6 1.4-4.7 4-8Z' /><path d='M12 13c1.2 1 2 2.1 2 3.3A2 2 0 1 1 10 16c0-1.2.8-2.3 2-3Z' /></svg>;
    case 'moon': return <svg {...common}><path d='M21 12.8A9 9 0 1 1 11.2 3 7 7 0 0 0 21 12.8Z' /></svg>;
    case 'sparkles':
    case 'wand-sparkles': return <svg {...common}><path d='M12 3v4' /><path d='M12 17v4' /><path d='M3 12h4' /><path d='M17 12h4' /><path d='m6 6 2.5 2.5' /><path d='M15.5 15.5 18 18' /><path d='m18 6-2.5 2.5' /><path d='M8.5 15.5 6 18' /></svg>;
    case 'flask-conical': return <svg {...common}><path d='M10 3v5l-5.5 9.5A2 2 0 0 0 6.2 20h11.6a2 2 0 0 0 1.7-2.5L14 8V3' /><path d='M8.5 13h7' /></svg>;
    case 'sword': return <svg {...common}><path d='m14.5 4.5 5 5' /><path d='M13 6 6 13' /><path d='m5 14 5 5' /><path d='M4 20h6' /><path d='M17 3h4v4' /></svg>;
    case 'swords': return <svg {...common}><path d='m14.5 4.5 5 5' /><path d='M13 6 6 13' /><path d='m5 14 5 5' /><path d='M4 20h6' /><path d='m9.5 4.5-5 5' /><path d='M11 6l7 7' /><path d='m19 14-5 5' /><path d='M14 20h6' /></svg>;
    default: return token;
  }
};
const renderIconContent = (value: unknown, familyName?: string): React.ReactNode => {
  const text = asText(value, '');
  if (!text || familyName?.trim().toLowerCase() !== 'lucide') return text;
  return renderLucideIcon(text);
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
    <div className='boomhud-node boomhud-component-charportrait' style={ { padding: '0', display: 'flex', flexDirection: 'column', gap: '8px', margin: '0', width: '130px', alignItems: 'center', justifyContent: 'flex-start', color: '#000000', backgroundColor: '#000000', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'Component/CharPortrait')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'Component/CharPortrait')}>
      <div className='boomhud-node boomhud-face' style={ { padding: '0', margin: '0', width: '56px', height: '56px', position: 'relative', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#000000', backgroundColor: '#000000', borderStyle: 'solid', borderWidth: '6px', borderColor: '#FFFFFF', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'face')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'face')}>
        <span className='boomhud-node boomhud-classicon' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', width: '32px', height: '32px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontSize: '32px', fontFamily: 'lucide', whiteSpace: 'nowrap', position: 'absolute', left: '12px', top: '12px', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'classIcon')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'classIcon')}>
          {renderIconContent(getMotionText(props.motionTargets, resolveMotionId(props.motionScope, 'classIcon')) ?? ('shield'), 'lucide')}
        </span>
      </div>
      <span className='boomhud-node boomhud-name' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', alignSelf: 'stretch', flex: '1 1 0', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontWeight: '400', fontSize: '8px', fontFamily: 'Press Start 2P', whiteSpace: 'nowrap', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'name')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'name')}>
        {getMotionText(props.motionTargets, resolveMotionId(props.motionScope, 'name')) ?? ('Name')}
      </span>
      <div className='boomhud-node boomhud-hp' style={ { padding: '0', margin: '0', alignSelf: 'stretch', height: '10px', position: 'relative', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#333333', backgroundColor: '#333333', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'hp')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'hp')}>
        <section className='boomhud-node boomhud-hpfill' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', width: '90px', height: '10px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#CCCCCC', backgroundColor: '#CCCCCC', position: 'absolute', left: '0px', top: '0px', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'hpFill')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'hpFill')} />
      </div>
      <div className='boomhud-node boomhud-mp' style={ { padding: '0', margin: '0', alignSelf: 'stretch', height: '8px', position: 'relative', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#333333', backgroundColor: '#333333', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'mp')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'mp')}>
        <section className='boomhud-node boomhud-mpfill' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', width: '60px', height: '8px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#777777', backgroundColor: '#777777', position: 'absolute', left: '0px', top: '0px', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'mpFill')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'mpFill')} />
      </div>
      <div className='boomhud-node boomhud-stats' style={ { padding: '0 6px', display: 'flex', flexDirection: 'row', gap: '8px', margin: '0', alignSelf: 'stretch', flex: '1 1 0', alignItems: 'flex-start', justifyContent: 'center', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'stats')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'stats')}>
        <span className='boomhud-node boomhud-stat1' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', flex: '1 1 0', alignSelf: 'stretch', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#AAAAAA', fontWeight: '400', fontSize: '8px', fontFamily: 'Press Start 2P', whiteSpace: 'nowrap', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'stat1')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'stat1')}>
          {getMotionText(props.motionTargets, resolveMotionId(props.motionScope, 'stat1')) ?? ('ATK 10')}
        </span>
        <span className='boomhud-node boomhud-stat2' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', flex: '1 1 0', alignSelf: 'stretch', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#AAAAAA', fontWeight: '400', fontSize: '8px', fontFamily: 'Press Start 2P', whiteSpace: 'nowrap', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'stat2')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'stat2')}>
          {getMotionText(props.motionTargets, resolveMotionId(props.motionScope, 'stat2')) ?? ('DEF 8')}
        </span>
      </div>
      <div className='boomhud-node boomhud-actiongrid' style={ { padding: '0', display: 'flex', flexDirection: 'row', gap: '2px', margin: '0', alignSelf: 'stretch', flex: '1 1 0', alignItems: 'flex-start', justifyContent: 'flex-start', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'actionGrid')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'actionGrid')}>
        <div className='boomhud-node boomhud-atk' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', flex: '1 1 0', height: '28px', alignItems: 'center', justifyContent: 'center', color: '#000000', backgroundColor: '#000000', borderStyle: 'solid', borderWidth: '2px', borderColor: '#FFFFFF', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'atk')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'atk')}>
          <span className='boomhud-node boomhud-qepo3' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', width: '16px', height: '16px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontSize: '16px', fontFamily: 'lucide', whiteSpace: 'nowrap', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'qEpO3')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'qEpO3')}>
            {renderIconContent(getMotionText(props.motionTargets, resolveMotionId(props.motionScope, 'qEpO3')) ?? ('swords'), 'lucide')}
          </span>
        </div>
        <div className='boomhud-node boomhud-mag' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', flex: '1 1 0', height: '28px', alignItems: 'center', justifyContent: 'center', color: '#000000', backgroundColor: '#000000', borderStyle: 'solid', borderWidth: '2px', borderColor: '#FFFFFF', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'mag')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'mag')}>
          <span className='boomhud-node boomhud-aiphn' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', width: '16px', height: '16px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontSize: '16px', fontFamily: 'lucide', whiteSpace: 'nowrap', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'AIphN')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'AIphN')}>
            {renderIconContent(getMotionText(props.motionTargets, resolveMotionId(props.motionScope, 'AIphN')) ?? ('wand-sparkles'), 'lucide')}
          </span>
        </div>
        <div className='boomhud-node boomhud-def' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', flex: '1 1 0', height: '28px', alignItems: 'center', justifyContent: 'center', color: '#000000', backgroundColor: '#000000', borderStyle: 'solid', borderWidth: '2px', borderColor: '#FFFFFF', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'def')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'def')}>
          <span className='boomhud-node boomhud-e4qkz' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', width: '16px', height: '16px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontSize: '16px', fontFamily: 'lucide', whiteSpace: 'nowrap', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'e4QKZ')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'e4QKZ')}>
            {renderIconContent(getMotionText(props.motionTargets, resolveMotionId(props.motionScope, 'e4QKZ')) ?? ('shield'), 'lucide')}
          </span>
        </div>
        <div className='boomhud-node boomhud-item' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', flex: '1 1 0', height: '28px', alignItems: 'center', justifyContent: 'center', color: '#000000', backgroundColor: '#000000', borderStyle: 'solid', borderWidth: '2px', borderColor: '#FFFFFF', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'item')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'item')}>
          <span className='boomhud-node boomhud-dvzx7' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', width: '16px', height: '16px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontSize: '16px', fontFamily: 'lucide', whiteSpace: 'nowrap', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'dVzX7')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'dVzX7')}>
            {renderIconContent(getMotionText(props.motionTargets, resolveMotionId(props.motionScope, 'dVzX7')) ?? ('flask-conical'), 'lucide')}
          </span>
        </div>
      </div>
    </div>
  );
}
export default CharPortraitView;
