import { Skeleton } from '@/components/ui/skeleton'
import { Card, CardContent, CardHeader } from '@/components/ui/card'

export function LoadingKpiCards({ count = 4 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      {Array.from({ length: count }).map((_, i) => (
        <Card key={i} className="kpi-card">
          <CardContent className="p-6">
            <Skeleton className="h-4 w-24 mb-3" />
            <Skeleton className="h-8 w-32" />
            <Skeleton className="h-4 w-20 mt-2" />
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

export function LoadingChart({ className }: { className?: string }) {
  return (
    <Card className={className}>
      <CardHeader className="pb-2">
        <Skeleton className="h-5 w-40" />
      </CardHeader>
      <CardContent>
        <div className="h-[300px] lg:h-[350px] flex items-center justify-center">
          <div className="text-center">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary mx-auto"></div>
            <p className="text-sm text-muted-foreground mt-2">Cargando datos...</p>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}

export function LoadingTable({ className, rows = 5 }: { className?: string; rows?: number }) {
  return (
    <Card className={className}>
      <CardHeader className="pb-2">
        <Skeleton className="h-5 w-48" />
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          {Array.from({ length: rows }).map((_, i) => (
            <div key={i} className="flex gap-4">
              <Skeleton className="h-4 flex-1" />
              <Skeleton className="h-4 w-24" />
              <Skeleton className="h-4 w-20" />
            </div>
          ))}
        </div>
      </CardContent>
    </Card>
  )
}

export function LoadingPage() {
  return (
    <div className="space-y-6">
      <LoadingKpiCards count={4} />
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <LoadingChart />
        <LoadingChart />
      </div>
      <LoadingTable rows={8} />
    </div>
  )
}
