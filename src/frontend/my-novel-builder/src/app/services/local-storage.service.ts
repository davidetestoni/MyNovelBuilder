import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class LocalStorageService {
  /**
   * Gets a string value from localStorage for the given key
   * @param key The key to retrieve
   * @returns The stored string value or null if not found
   */
  getStringForKey(key: string): string | null {
    return localStorage.getItem(key);
  }

  /**
   * Sets a string value in localStorage for the given key
   * @param key The key to set
   * @param value The string value to store
   */
  setStringForKey(key: string, value: string): void {
    localStorage.setItem(key, value);
  }

  /**
   * Gets and parses an object from localStorage for the given key
   * @param key The key to retrieve
   * @returns The parsed object of type T or null if not found
   */
  getObjectForKey<T>(key: string): T | null {
    const item = localStorage.getItem(key);
    return item === null ? null : this.parseJson<T>(item);
  }

  /**
   * Sets an object in localStorage for the given key
   * @param key The key to set
   * @param value The object to stringify and store
   */
  setObjectForKey<T>(key: string, value: T): void {
    localStorage.setItem(key, JSON.stringify(value));
  }

  /**
   * Gets a value from a map-like object stored in localStorage
   * @param storageKey The key for the localStorage item
   * @param mapKey The key within the stored map object
   * @returns The value from the map or null if not found
   */
  getNestedStringForKey(storageKey: string, mapKey: string): string | null {
    const map = this.getStringMapForKey(storageKey);
    return map?.[mapKey] ?? null;
  }

  /**
   * Sets a value in a map-like object stored in localStorage
   * @param storageKey The key for the localStorage item
   * @param mapKey The key within the stored map object
   * @param value The value to store
   */
  setNestedStringForKey(
    storageKey: string,
    mapKey: string,
    value: string,
  ): void {
    const map = this.getStringMapForKey(storageKey) ?? {};
    map[mapKey] = value;
    this.setObjectForKey(storageKey, map);
  }

  /**
   * Gets a typed value from a map-like object stored in localStorage
   * @param storageKey The key for the localStorage item
   * @param mapKey The key within the stored map object
   * @returns The parsed value from the map or null if not found
   */
  getNestedObjectForKey<T>(storageKey: string, mapKey: string): T | null {
    const value = this.getStringMapForKey(storageKey)?.[mapKey];
    return value === undefined ? null : this.parseJson<T>(value);
  }

  /**
   * Gets a string array from a map-like object stored in localStorage
   * @param storageKey The key for the localStorage item
   * @param mapKey The key within the stored map object
   * @returns The parsed string array or an empty array if not found/invalid
   */
  getNestedStringArrayForKey(storageKey: string, mapKey: string): string[] {
    const value = this.getNestedObjectForKey<unknown>(storageKey, mapKey);
    if (!Array.isArray(value)) {
      return [];
    }

    return value.filter((item): item is string => typeof item === 'string');
  }

  /**
   * Sets a typed value in a map-like object stored in localStorage
   * @param storageKey The key for the localStorage item
   * @param mapKey The key within the stored map object
   * @param value The value to stringify and store
   */
  setNestedObjectForKey<T>(storageKey: string, mapKey: string, value: T): void {
    const map = this.getStringMapForKey(storageKey) ?? {};
    map[mapKey] = JSON.stringify(value);
    this.setObjectForKey(storageKey, map);
  }

  /**
   * Pushes a string value to the front of a nested recent-items list
   * @param storageKey The key for the localStorage item
   * @param mapKey The key within the stored map object
   * @param value The value to insert
   * @param maxItems The maximum number of items to keep
   */
  pushNestedRecentStringForKey(
    storageKey: string,
    mapKey: string,
    value: string,
    maxItems = 5,
  ): void {
    if (!value.trim()) {
      return;
    }

    const items = this.getNestedStringArrayForKey(storageKey, mapKey).filter(
      (item) => item !== value,
    );
    items.unshift(value);

    this.setNestedObjectForKey(storageKey, mapKey, items.slice(0, maxItems));
  }

  /**
   * Removes a key from a map-like object stored in localStorage
   * @param storageKey The key for the localStorage item
   * @param mapKey The key to remove from the stored map object
   */
  removeNestedKey(storageKey: string, mapKey: string): void {
    const map = this.getStringMapForKey(storageKey);
    if (map && mapKey in map) {
      delete map[mapKey];
      this.setObjectForKey(storageKey, map);
    }
  }

  /**
   * Checks if a key exists in a map-like object stored in localStorage
   * @param storageKey The key for the localStorage item
   * @param mapKey The key to check in the stored map object
   * @returns boolean indicating if the nested key exists
   */
  hasNestedKey(storageKey: string, mapKey: string): boolean {
    const map = this.getStringMapForKey(storageKey);
    return map !== null && mapKey in map;
  }

  /**
   * Removes an item from localStorage
   * @param key The key to remove
   */
  removeItem(key: string): void {
    localStorage.removeItem(key);
  }

  /**
   * Checks if a key exists in localStorage
   * @param key The key to check
   * @returns boolean indicating if the key exists
   */
  hasKey(key: string): boolean {
    return localStorage.getItem(key) !== null;
  }

  /**
   * Clears all data from localStorage
   */
  clear(): void {
    localStorage.clear();
  }

  private getStringMapForKey(key: string): Record<string, string> | null {
    const value = this.getObjectForKey<unknown>(key);
    if (!this.isRecord(value)) {
      return null;
    }

    return Object.fromEntries(
      Object.entries(value).filter(
        (entry): entry is [string, string] => typeof entry[1] === 'string',
      ),
    );
  }

  private isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
  }

  private parseJson<T>(value: string): T | null {
    try {
      return JSON.parse(value) as T;
    } catch {
      return null;
    }
  }
}
