interface ImportMeta {
  glob(
    pattern: string,
    options: { eager: boolean },
  ): Record<string, Record<string, unknown>>;
}
