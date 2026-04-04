/**
 * DOMMatrix polyfill for Node.js build workers
 *
 * Issue #1817: pdfjs-dist and framer-motion require DOMMatrix which doesn't
 * exist in Node.js. This preload script ensures the polyfill is available
 * in ALL processes including Turbopack build workers.
 *
 * Loaded via NODE_OPTIONS="--require ./scripts/dommatrix-polyfill.js"
 */

if (typeof globalThis !== 'undefined' && !globalThis.DOMMatrix) {
  globalThis.DOMMatrix = class DOMMatrix {
    constructor(init) {
      this.m11 = 1;
      this.m12 = 0;
      this.m13 = 0;
      this.m14 = 0;
      this.m21 = 0;
      this.m22 = 1;
      this.m23 = 0;
      this.m24 = 0;
      this.m31 = 0;
      this.m32 = 0;
      this.m33 = 1;
      this.m34 = 0;
      this.m41 = 0;
      this.m42 = 0;
      this.m43 = 0;
      this.m44 = 1;
      this.a = 1;
      this.b = 0;
      this.c = 0;
      this.d = 1;
      this.e = 0;
      this.f = 0;
      this.is2D = true;
      this.isIdentity = true;

      if (Array.isArray(init) && init.length === 16) {
        [
          this.m11,
          this.m12,
          this.m13,
          this.m14,
          this.m21,
          this.m22,
          this.m23,
          this.m24,
          this.m31,
          this.m32,
          this.m33,
          this.m34,
          this.m41,
          this.m42,
          this.m43,
          this.m44,
        ] = init;
        this.a = this.m11;
        this.b = this.m12;
        this.c = this.m21;
        this.d = this.m22;
        this.e = this.m41;
        this.f = this.m42;
      }
    }

    static fromMatrix() {
      return new DOMMatrix();
    }
    static fromFloat32Array(a) {
      return new DOMMatrix(Array.from(a));
    }
    static fromFloat64Array(a) {
      return new DOMMatrix(Array.from(a));
    }

    translate() {
      return this;
    }
    scale() {
      return this;
    }
    scaleNonUniform() {
      return this;
    }
    scale3d() {
      return this;
    }
    rotate() {
      return this;
    }
    rotateFromVector() {
      return this;
    }
    rotateAxisAngle() {
      return this;
    }
    skewX() {
      return this;
    }
    skewY() {
      return this;
    }
    multiply() {
      return this;
    }
    flipX() {
      return this;
    }
    flipY() {
      return this;
    }
    inverse() {
      return this;
    }
    transformPoint() {
      return { x: 0, y: 0, z: 0, w: 1 };
    }

    toFloat32Array() {
      return new Float32Array([
        this.m11,
        this.m12,
        this.m13,
        this.m14,
        this.m21,
        this.m22,
        this.m23,
        this.m24,
        this.m31,
        this.m32,
        this.m33,
        this.m34,
        this.m41,
        this.m42,
        this.m43,
        this.m44,
      ]);
    }
    toFloat64Array() {
      return new Float64Array([
        this.m11,
        this.m12,
        this.m13,
        this.m14,
        this.m21,
        this.m22,
        this.m23,
        this.m24,
        this.m31,
        this.m32,
        this.m33,
        this.m34,
        this.m41,
        this.m42,
        this.m43,
        this.m44,
      ]);
    }
    toJSON() {
      return {
        a: this.a,
        b: this.b,
        c: this.c,
        d: this.d,
        e: this.e,
        f: this.f,
        m11: this.m11,
        m12: this.m12,
        m13: this.m13,
        m14: this.m14,
        m21: this.m21,
        m22: this.m22,
        m23: this.m23,
        m24: this.m24,
        m31: this.m31,
        m32: this.m32,
        m33: this.m33,
        m34: this.m34,
        m41: this.m41,
        m42: this.m42,
        m43: this.m43,
        m44: this.m44,
        is2D: this.is2D,
        isIdentity: this.isIdentity,
      };
    }
    toString() {
      if (this.is2D)
        return `matrix(${this.a}, ${this.b}, ${this.c}, ${this.d}, ${this.e}, ${this.f})`;
      return `matrix3d(${this.m11}, ${this.m12}, ${this.m13}, ${this.m14}, ${this.m21}, ${this.m22}, ${this.m23}, ${this.m24}, ${this.m31}, ${this.m32}, ${this.m33}, ${this.m34}, ${this.m41}, ${this.m42}, ${this.m43}, ${this.m44})`;
    }
  };
}

if (typeof globalThis !== 'undefined' && !globalThis.DOMPoint) {
  globalThis.DOMPoint = class DOMPoint {
    constructor(x = 0, y = 0, z = 0, w = 1) {
      this.x = x;
      this.y = y;
      this.z = z;
      this.w = w;
    }
    static fromPoint(other) {
      return new DOMPoint(other?.x, other?.y, other?.z, other?.w);
    }
    matrixTransform() {
      return this;
    }
    toJSON() {
      return { x: this.x, y: this.y, z: this.z, w: this.w };
    }
  };
}
