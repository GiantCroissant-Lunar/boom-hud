import React from 'react';
export interface StatusIconViewModel {
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

export function StatusIconView(props: StatusIconViewModel): React.JSX.Element {
  return (
    <div className='boomhud-node boomhud-component-statusicon' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', width: '24px', height: '24px', alignItems: 'center', justifyContent: 'center', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'Component/StatusIcon') } } data-boomhud-id='Component/StatusIcon'>
      <span className='boomhud-node boomhud-icon' style={ { display: 'flex', flexDirection: 'column', padding: '0', margin: '0', width: '24px', height: '24px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontFamily: 'lucide', fontWeight: '400', ...getMotionStyle(props.motionTargets, 'icon') } } data-boomhud-id='icon'>
        {resolveIconText(getMotionText(props.motionTargets, 'icon') ?? ('flame'), 'lucide')}
      </span>
    </div>
  );
}
export default StatusIconView;
