/**
 * Next.js Instrumentation
 *
 * Issue #1817, #2618: DOMMatrix polyfill for SSR compatibility
 * ----------------------------------------------------------
 * This file runs BEFORE the application code is loaded, making it
 * ideal for polyfills required by libraries like pdfjs-dist and framer-motion.
 *
 * The DOMMatrix polyfill ensures these libraries can be imported during
 * Next.js static generation (SSG) without throwing ReferenceError.
 *
 * @see https://nextjs.org/docs/app/building-your-application/optimizing/instrumentation
 */

export async function register() {
  // Only run in Node.js environment (server-side)
  if (typeof window === 'undefined' && typeof globalThis !== 'undefined') {
    // DOMMatrix polyfill for pdfjs-dist and framer-motion SSR
    if (!globalThis.DOMMatrix) {
      globalThis.DOMMatrix = class DOMMatrix {
        m11 = 1;
        m12 = 0;
        m13 = 0;
        m14 = 0;
        m21 = 0;
        m22 = 1;
        m23 = 0;
        m24 = 0;
        m31 = 0;
        m32 = 0;
        m33 = 1;
        m34 = 0;
        m41 = 0;
        m42 = 0;
        m43 = 0;
        m44 = 1;
        a = 1;
        b = 0;
        c = 0;
        d = 1;
        e = 0;
        f = 0;
        is2D = true;
        isIdentity = true;

        constructor(init?: number[] | string) {
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

        static fromMatrix(_other?: DOMMatrixInit): DOMMatrix {
          return new DOMMatrix();
        }

        static fromFloat32Array(array32: Float32Array): DOMMatrix {
          return new DOMMatrix(Array.from(array32));
        }

        static fromFloat64Array(array64: Float64Array): DOMMatrix {
          return new DOMMatrix(Array.from(array64));
        }

        translate(_tx?: number, _ty?: number, _tz?: number): DOMMatrix {
          return this;
        }

        scale(
          _scaleX?: number,
          _scaleY?: number,
          _scaleZ?: number,
          _originX?: number,
          _originY?: number,
          _originZ?: number
        ): DOMMatrix {
          return this;
        }

        scaleNonUniform(_scaleX?: number, _scaleY?: number): DOMMatrix {
          return this;
        }

        scale3d(
          _scale?: number,
          _originX?: number,
          _originY?: number,
          _originZ?: number
        ): DOMMatrix {
          return this;
        }

        rotate(_rotX?: number, _rotY?: number, _rotZ?: number): DOMMatrix {
          return this;
        }

        rotateFromVector(_x?: number, _y?: number): DOMMatrix {
          return this;
        }

        rotateAxisAngle(
          _x?: number,
          _y?: number,
          _z?: number,
          _angle?: number
        ): DOMMatrix {
          return this;
        }

        skewX(_sx?: number): DOMMatrix {
          return this;
        }

        skewY(_sy?: number): DOMMatrix {
          return this;
        }

        multiply(_other?: DOMMatrixInit): DOMMatrix {
          return this;
        }

        flipX(): DOMMatrix {
          return this;
        }

        flipY(): DOMMatrix {
          return this;
        }

        inverse(): DOMMatrix {
          return this;
        }

        transformPoint(_point?: DOMPointInit): DOMPoint {
          // Returns a minimal DOMPoint-like object
          const result: DOMPoint = {
            x: 0,
            y: 0,
            z: 0,
            w: 1,
            matrixTransform: () => result,
            toJSON: () => ({ x: 0, y: 0, z: 0, w: 1 }),
          };
          return result;
        }

        toFloat32Array(): Float32Array {
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

        toFloat64Array(): Float64Array {
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

        toJSON(): object {
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

        toString(): string {
          if (this.is2D) {
            return `matrix(${this.a}, ${this.b}, ${this.c}, ${this.d}, ${this.e}, ${this.f})`;
          }
          return `matrix3d(${this.m11}, ${this.m12}, ${this.m13}, ${this.m14}, ${this.m21}, ${this.m22}, ${this.m23}, ${this.m24}, ${this.m31}, ${this.m32}, ${this.m33}, ${this.m34}, ${this.m41}, ${this.m42}, ${this.m43}, ${this.m44})`;
        }
      };

      console.warn('[Instrumentation] DOMMatrix polyfill registered');
    }

    // DOMPoint polyfill (used by DOMMatrix.transformPoint)
    if (!globalThis.DOMPoint) {
      globalThis.DOMPoint = class DOMPoint {
        x: number;
        y: number;
        z: number;
        w: number;

        constructor(x = 0, y = 0, z = 0, w = 1) {
          this.x = x;
          this.y = y;
          this.z = z;
          this.w = w;
        }

        static fromPoint(other?: DOMPointInit): DOMPoint {
          return new DOMPoint(other?.x, other?.y, other?.z, other?.w);
        }

        matrixTransform(_matrix?: DOMMatrixInit): DOMPoint {
          return this;
        }

        toJSON(): object {
          return { x: this.x, y: this.y, z: this.z, w: this.w };
        }
      };

      console.warn('[Instrumentation] DOMPoint polyfill registered');
    }
  }
}
