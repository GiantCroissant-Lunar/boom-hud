export interface CharPortraitViewModel {
  motionTargets?: Record<string, Partial<Record<'opacity' | 'positionX' | 'positionY' | 'positionZ' | 'scaleX' | 'scaleY' | 'scaleZ' | 'rotation' | 'rotationX' | 'rotationY' | 'width' | 'height' | 'visibility' | 'text' | 'spriteFrame' | 'color', number | boolean | string>>>;
  motionScope?: string;
}
