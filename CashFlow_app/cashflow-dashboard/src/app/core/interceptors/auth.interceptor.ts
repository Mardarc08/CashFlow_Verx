import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiYWRtaW4iOnRydWUsImlhdCI6MTUxNjIzOTAyMiwiZXhwIjoxNzc4ODM5NjAwLCJhdWQiOiJjYXNoZmxvdy1jbGllbnRzIiwiaXNzIjoiY2FzaGZsb3ctYXBpIn0.MDE_l4E53QigTVhYLa4GXJvRzAtFgE2R3Sa4g06CY9I";

  if (token) {
    const authReq = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
    return next(authReq);
  }

  return next(req);
};
