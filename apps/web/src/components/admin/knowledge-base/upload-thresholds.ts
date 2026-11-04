/**
 * #4446: above this size the admin KB upload zone sends a PDF in chunks. The Processing Settings
 * panel shows it from here, so the number it shows is the one the zone uses.
 */
export const MAX_SINGLE_UPLOAD_SIZE = 10 * 1024 * 1024;
