import React from 'react';
export interface ActionButtonViewModel {
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

export function ActionButtonView(props: ActionButtonViewModel): React.JSX.Element {
  return (
    <div className='boomhud-node boomhud-component-actionbutton' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', width: '28px', height: '28px', alignItems: 'center', justifyContent: 'center', color: '#000000', backgroundColor: '#000000', borderStyle: 'solid', borderWidth: '2px', borderColor: '#FFFFFF', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'Component/ActionButton')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'Component/ActionButton')}>
      <span className='boomhud-node boomhud-icon' style={ { padding: '0', display: 'flex', flexDirection: 'column', margin: '0', width: '16px', height: '16px', alignItems: 'flex-start', justifyContent: 'flex-start', color: '#FFFFFF', fontSize: '16px', fontFamily: 'lucide', whiteSpace: 'nowrap', ...getMotionStyle(props.motionTargets, resolveMotionId(props.motionScope, 'icon')) } } data-boomhud-id={resolveMotionId(props.motionScope, 'icon')}>
        {renderIconContent(getMotionText(props.motionTargets, resolveMotionId(props.motionScope, 'icon')) ?? ('swords'), 'lucide')}
      </span>
    </div>
  );
}
export default ActionButtonView;
