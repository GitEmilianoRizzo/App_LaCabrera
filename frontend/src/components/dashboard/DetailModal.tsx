import { X } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { formatCurrency, formatNumber } from '@/lib/utils'

interface DetailModalProps {
  isOpen: boolean
  onClose: () => void
  title: string
  subtitle?: string
  data: DetailItem[]
  currency?: string
}

interface DetailItem {
  label: string
  value: string | number
  format?: 'currency' | 'number' | 'percent' | 'text'
  highlight?: boolean
}

export function DetailModal({ isOpen, onClose, title, subtitle, data, currency = 'USD' }: DetailModalProps) {
  if (!isOpen) return null

  const formatValue = (item: DetailItem) => {
    if (item.format === 'currency') {
      return formatCurrency(item.value as number, currency)
    }
    if (item.format === 'number') {
      return formatNumber(item.value as number)
    }
    if (item.format === 'percent') {
      return `${formatNumber(item.value as number, 1)}%`
    }
    return item.value
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Modal */}
      <Card className="relative z-10 w-full max-w-lg mx-4 max-h-[80vh] overflow-auto shadow-2xl">
        <CardHeader className="pb-2 border-b">
          <div className="flex items-start justify-between">
            <div>
              <CardTitle className="text-lg">{title}</CardTitle>
              {subtitle && (
                <p className="text-sm text-muted-foreground mt-1">{subtitle}</p>
              )}
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={onClose}
              className="h-8 w-8 p-0"
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
        </CardHeader>
        <CardContent className="pt-4">
          <div className="space-y-3">
            {data.map((item, index) => (
              <div
                key={index}
                className={`flex justify-between items-center py-2 ${
                  index < data.length - 1 ? 'border-b border-gray-100 dark:border-gray-700' : ''
                } ${item.highlight ? 'bg-cabrera-burgundy/5 dark:bg-cabrera-burgundy/10 -mx-2 px-2 rounded' : ''}`}
              >
                <span className="text-sm text-muted-foreground">{item.label}</span>
                <span className={`font-medium ${item.highlight ? 'text-cabrera-burgundy' : ''}`}>
                  {formatValue(item)}
                </span>
              </div>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  )
}

// Hook para manejar el estado del modal
import { useState, useCallback } from 'react'

export interface ModalState {
  isOpen: boolean
  title: string
  subtitle?: string
  data: DetailItem[]
  currency?: string
}

export function useDetailModal() {
  const [modalState, setModalState] = useState<ModalState>({
    isOpen: false,
    title: '',
    data: [],
  })

  const openModal = useCallback((state: Omit<ModalState, 'isOpen'>) => {
    setModalState({ ...state, isOpen: true })
  }, [])

  const closeModal = useCallback(() => {
    setModalState(prev => ({ ...prev, isOpen: false }))
  }, [])

  return { modalState, openModal, closeModal }
}
